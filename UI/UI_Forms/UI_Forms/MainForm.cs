using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using UI_Forms.Models;

namespace UI_Forms
{
    public partial class MainForm : Form
    {
        private DateTime _currentDate = DateTime.Today;
        private List<DayControl> _dayControls = new List<DayControl>();
        private bool _isWeeklyView = false;

        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            string userName = ApiService.CurrentUserName ?? "사용자";
            lblTitle.Text = $"{userName}님의 개인 캘린더";

            // 뷰 타입 초기화
            cmbViewType.SelectedIndexChanged -= cmbViewType_SelectedIndexChanged;
            cmbViewType.Items.Clear();
            cmbViewType.Items.Add("월간 뷰");
            cmbViewType.Items.Add("주간 뷰");
            cmbViewType.SelectedIndex = 0;
            cmbViewType.SelectedIndexChanged += cmbViewType_SelectedIndexChanged;

            InitializeCalendarGrid();
            await LoadUserTeamsAsync();
            await RenderCalendarAsync();
        }

        private async Task LoadUserTeamsAsync()
        {
            try
            {
                string userId = ApiService.CurrentUserId;
                var response = await ApiService.GetAsync<ApiResponse<List<TeamDto>>>($"/api/teams/{userId}");

                List<TeamDto> displayTeams = new List<TeamDto>();
                displayTeams.Add(new TeamDto { TeamId = "PERSONAL", TeamName = "개인 캘린더" });

                if (response?.Success == true && response.Data != null)
                {
                    var uniqueTeams = response.Data.GroupBy(t => t.TeamName).Select(g => g.First()).ToList();
                    displayTeams.AddRange(uniqueTeams);
                }

                cmbTeams.SelectedIndexChanged -= cmbTeams_SelectedIndexChanged;
                cmbTeams.DataSource = displayTeams;
                cmbTeams.DisplayMember = "TeamName";
                cmbTeams.ValueMember = "TeamId";
                cmbTeams.SelectedIndexChanged += cmbTeams_SelectedIndexChanged;
            }
            catch { /* 오류 처리 생략 */ }
        }

        private void InitializeCalendarGrid()
        {
            pnlCalendar.Controls.Clear();
            _dayControls.Clear();
            for (int i = 0; i < 42; i++)
            {
                DayControl dc = new DayControl();
                _dayControls.Add(dc);
                pnlCalendar.Controls.Add(dc);
            }
        }

        private async Task RenderCalendarAsync()
        {
            int totalDaysToRender = _isWeeklyView ? 7 : 42;
            DateTime startDate;

            if (_isWeeklyView)
            {
                int currentDayOfWeek = (int)_currentDate.DayOfWeek;
                startDate = _currentDate.AddDays(-currentDayOfWeek);
                lblCurrentMonth.Text = $"{startDate:yyyy년 MM월 dd일} ~ {startDate.AddDays(6):dd일}";
            }
            else
            {
                lblCurrentMonth.Text = _currentDate.ToString("yyyy년 MM월");
                DateTime firstDayOfMonth = new DateTime(_currentDate.Year, _currentDate.Month, 1);
                int startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
                startDate = firstDayOfMonth.AddDays(-startDayOfWeek);
            }

            int colWidth = pnlCalendar.Width / 7;
            int rowHeight = _isWeeklyView ? pnlCalendar.Height : (pnlCalendar.Height / 6);

            for (int i = 0; i < 42; i++)
            {
                if (i < totalDaysToRender)
                {
                    _dayControls[i].Visible = true;
                    _dayControls[i].Size = new Size(colWidth, rowHeight);
                    _dayControls[i].Location = new Point((i % 7) * colWidth, (i / 7) * rowHeight);
                    DateTime cellDate = startDate.AddDays(i);
                    _dayControls[i].SetDay(cellDate, _isWeeklyView || (cellDate.Month == _currentDate.Month));
                }
                else _dayControls[i].Visible = false;
            }

            await FetchAndRenderSchedulesAsync(startDate, totalDaysToRender);
        }

        private async Task FetchAndRenderSchedulesAsync(DateTime startDate, int totalDays)
        {
            try
            {
                string userId = ApiService.CurrentUserId;
                string selectedTeamId = cmbTeams.SelectedValue?.ToString() ?? "PERSONAL";
                bool isTeamView = selectedTeamId != "PERSONAL";

                var fetchTasks = new List<Task>();
                for (int i = 0; i < totalDays; i++)
                {
                    int index = i;
                    DateTime targetDate = startDate.AddDays(index);
                    string dateStr = targetDate.ToString("yyyy-MM-dd");

                    fetchTasks.Add(Task.Run(async () =>
                    {
                        ApiResponse<AvailabilityData> res;
                        if (isTeamView)
                            res = await ApiService.GetAsync<ApiResponse<AvailabilityData>>($"/api/availability/team/{selectedTeamId}/{dateStr}");
                        else
                            res = await ApiService.GetAsync<ApiResponse<AvailabilityData>>($"/api/availability/{userId}/{dateStr}");

                        if (res?.Success == true && res.Data?.Slots != null)
                        {
                            this.Invoke(new Action(() =>
                            {
                                foreach (var slot in res.Data.Slots)
                                {
                                    // 🌟 [조건 1] 색상 결정: 팀 일정은 무조건 LightCoral, 개인은 저장된 색(없으면 기본색)
                                    Color scheduleColor = isTeamView ? Color.LightCoral : Color.CornflowerBlue;

                                    // 🌟 [조건 2] 형태 결정: 시작일과 종료일이 다르면 FullBox
                                    bool isFullBox = false;
                                    if (DateTime.TryParse(slot.Start, out DateTime sTime) && DateTime.TryParse(slot.End, out DateTime eTime))
                                    {
                                        if (sTime.Date != eTime.Date) isFullBox = true;
                                    }

                                    // 일정 렌더링 호출
                                    _dayControls[index].AddScheduleSlot(slot.Start + " 일정", scheduleColor, isFullBox);
                                }
                            }));
                        }
                    }));
                }
                await Task.WhenAll(fetchTasks);
            }
            catch { }
        }

        private async void cmbViewType_SelectedIndexChanged(object sender, EventArgs e)
        {
            _isWeeklyView = cmbViewType.Text == "주간 뷰";
            await RenderCalendarAsync();
        }

        private async void cmbTeams_SelectedIndexChanged(object sender, EventArgs e)
        {
            string userName = ApiService.CurrentUserName ?? "사용자";
            lblTitle.Text = cmbTeams.Text == "개인 캘린더" ? $"{userName}님의 개인 캘린더" : $"{userName}님의 [{cmbTeams.Text}] 팀 캘린더";
            await RenderCalendarAsync();
        }

        private async void btnPrev_Click(object sender, EventArgs e)
        {
            _currentDate = _isWeeklyView ? _currentDate.AddDays(-7) : _currentDate.AddMonths(-1);
            await RenderCalendarAsync();
        }

        private async void btnNext_Click(object sender, EventArgs e)
        {
            _currentDate = _isWeeklyView ? _currentDate.AddDays(7) : _currentDate.AddMonths(1);
            await RenderCalendarAsync();
        }

        private async void btnToday_Click(object sender, EventArgs e)
        {
            _currentDate = DateTime.Today;
            await RenderCalendarAsync();
        }

        private async void btnAddSchedule_Click(object sender, EventArgs e)
        {
            using (AddScheduleForm addForm = new AddScheduleForm())
            {
                if (addForm.ShowDialog() == DialogResult.OK) await RenderCalendarAsync();
            }
        }
    }
}