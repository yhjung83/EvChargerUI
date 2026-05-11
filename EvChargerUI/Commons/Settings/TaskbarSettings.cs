namespace EvChargerUI.Commons.Settings
{
    /// <summary>
    /// 작업표시줄 활성/비활성 설정
    /// </summary>
    public class TaskbarSettings
    {
        private bool _isEnabled;

        /// <summary>
        /// 작업표시줄 활성화 여부 (true: 활성, false: 비활성)
        /// 기본값: false (프로그램 시작 시 작업표시줄 비활성화)
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public TaskbarSettings()
        {
            _isEnabled = false; // 기본값: 비활성
        }
    }
}
