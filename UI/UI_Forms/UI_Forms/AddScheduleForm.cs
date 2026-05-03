using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using UI_Forms.Models;

namespace UI_Forms
{
    public partial class AddScheduleForm : Form
    {
        private Color _selectedColor = Color.CornflowerBlue;
        private readonly List<Color> _availableColors = new List<Color> {
            Color.CornflowerBlue, Color.MediumSeaGreen, Color.Orange, Color.MediumPurple, Color.Gray
        };

        private const string PlaceholderTitle = "일정 제목을 입력하세요";

        public AddScheduleForm()
        {
            InitializeComponent();
            SetupTimeComboBoxes();
            SetupColorPicker();
            SetupPlaceholder();

            // 기본값 설정 (시작일/종료일 오늘로 통일)
            dtpStartDate.Value = DateTime.Today;
            dtpEndDate.Value = DateTime.Today;
        }

        private void SetupPlaceholder()
        {
            txtTitle.Text = PlaceholderTitle;
            txtTitle.ForeColor = Color.Gray;

            txtTitle.Enter += (s, e) => {
                if (txtTitle.Text == PlaceholderTitle)
                {
                    txtTitle.Text = "";
                    txtTitle.ForeColor = Color.Black;
                }
            };

            txtTitle.Leave += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    txtTitle.Text = PlaceholderTitle;
                    txtTitle.ForeColor = Color.Gray;
                }
            };
        }

        private void SetupTimeComboBoxes()
        {
            cmbStartTime.Items.Clear();
            cmbEndTime.Items.Clear();

            for (int h = 0; h < 24; h++)
            {
                for (int m = 0; m < 60; m += 30)
                {
                    string time = $"{h:D2}:{m:D2}";
                    cmbStartTime.Items.Add(time);
                    cmbEndTime.Items.Add(time);
                }
            }

            cmbStartTime.SelectedIndex = 18; // 기본 09:00
            cmbEndTime.SelectedIndex = 20;   // 기본 10:00
        }

        private void SetupColorPicker()
        {
            foreach (var color in _availableColors)
            {
                Button btnColor = new Button
                {
                    Size = new Size(30, 30),
                    BackColor = color,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Tag = color
                };
                btnColor.FlatAppearance.BorderSize = 0;
                btnColor.Click += (s, e) => {
                    _selectedColor = (Color)((Button)s).Tag;
                    lblSelectedColor.BackColor = _selectedColor;
                };
                flpColors.Controls.Add(btnColor);
            }

            Button btnRedDisabled = new Button
            {
                Size = new Size(30, 30),
                BackColor = Color.LightCoral,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                Text = "X",
                ForeColor = Color.White
            };
            flpColors.Controls.Add(btnRedDisabled);
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || txtTitle.Text == PlaceholderTitle)
            {
                MessageBox.Show("일정 제목을 입력해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 날짜 및 시간 계산
            TimeSpan sTime = TimeSpan.Parse(cmbStartTime.Text);
            TimeSpan eTime = TimeSpan.Parse(cmbEndTime.Text);
            DateTime startDateTime = dtpStartDate.Value.Date.Add(sTime);
            DateTime endDateTime = dtpEndDate.Value.Date.Add(eTime);

            // 🌟 유효성 검사: 종료 시간이 시작 시간보다 앞서거나 같으면 차단
            if (startDateTime >= endDateTime)
            {
                MessageBox.Show("종료 시간은 시작 시간 이후여야 합니다.", "시간 설정 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSave.Enabled = false;

            try
            {
                bool allSuccess = true;
                DateTime currentDate = startDateTime.Date;
                DateTime endDate = endDateTime.Date;

                // 🌟 멀티 데이 처리 로직: 여러 날짜에 걸친 일정이면 하루 단위로 쪼개서 서버로 전송
                while (currentDate <= endDate)
                {
                    string slotStart = (currentDate == startDateTime.Date) ? cmbStartTime.Text : "00:00";
                    string slotEnd = (currentDate == endDate) ? cmbEndTime.Text : "23:59";

                    // 끝나는 날의 시간이 00:00 이라면, 그 날짜에는 일정이 없는 것으로 간주하고 스킵
                    if (slotStart == "00:00" && slotEnd == "00:00")
                    {
                        currentDate = currentDate.AddDays(1);
                        continue;
                    }

                    var request = new
                    {
                        userId = ApiService.CurrentUserId,
                        date = currentDate.ToString("yyyy-MM-dd"),
                        slots = new[] {
                            new { start = slotStart, end = slotEnd }
                        }
                    };

                    var response = await ApiService.PostAsync<object, ApiResponse<object>>("/api/availability", request);
                    if (response == null || !response.Success)
                    {
                        allSuccess = false;
                        MessageBox.Show($"[{currentDate:yyyy-MM-dd}] 저장 실패: {response?.Error}", "오류");
                    }

                    currentDate = currentDate.AddDays(1);
                }

                // 🌟 생성 완료 응답(성공)을 받으면 다이얼로그 표시 후 모달 종료
                if (allSuccess)
                {
                    MessageBox.Show("일정이 성공적으로 등록되었습니다.", "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK; // 부모 창(MainForm)에 성공을 알림
                    this.Close(); // 확인을 누르면 모달 창 닫힘
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"통신 중 오류 발생: {ex.Message}", "네트워크 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSave.Enabled = true;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e) => this.Close();
    }
}