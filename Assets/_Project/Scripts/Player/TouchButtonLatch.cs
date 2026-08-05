namespace Morae.Game.Player
{
    /// <summary>
    /// 터치 버튼의 Down/Held/Up 엣지 래치 (순수 로직 — EditMode 테스트 대상).
    /// <para>
    /// 왜 필요한가: 터치 UI가 상태를 갱신하는 시점(포인터 이벤트)과 게임 로직이 입력을 읽는 시점(각 Update)의
    /// 순서가 보장되지 않는다. 프레임 번호로 엣지를 래치해 <b>한 프레임 안에서는 모든 소비자가 같은 값</b>을 보고,
    /// 늦게 도착한 눌림도 다음 프레임에 정확히 1회만 Down으로 소비되게 한다 (유실·중복 없음).
    /// </para>
    /// 프레임 번호는 호출자가 넘긴다 (런타임은 Time.frameCount, 테스트는 임의 정수).
    /// </summary>
    public sealed class TouchButtonLatch
    {
        private bool _held;
        private bool _pendingDown;
        private bool _pendingUp;
        private bool _frameDown;
        private bool _frameUp;
        private int _syncedFrame = int.MinValue;

        public bool Held => _held;

        /// <summary>포인터 상태 갱신. 변화가 있을 때만 엣지를 예약한다.</summary>
        public void Set(bool pressed)
        {
            if (pressed == _held) return;
            _held = pressed;
            if (pressed) _pendingDown = true;
            else _pendingUp = true;
        }

        public bool Down(int frame)
        {
            Sync(frame);
            return _frameDown;
        }

        public bool Up(int frame)
        {
            Sync(frame);
            return _frameUp;
        }

        /// <summary>씬 리로드·비활성화 시 전량 초기화 (정적 보유 상태 잔존 방지).</summary>
        public void Reset()
        {
            _held = false;
            _pendingDown = false;
            _pendingUp = false;
            _frameDown = false;
            _frameUp = false;
            _syncedFrame = int.MinValue;
        }

        private void Sync(int frame)
        {
            if (_syncedFrame == frame) return;
            _syncedFrame = frame;
            _frameDown = _pendingDown;
            _frameUp = _pendingUp;
            _pendingDown = false;
            _pendingUp = false;
        }
    }
}
