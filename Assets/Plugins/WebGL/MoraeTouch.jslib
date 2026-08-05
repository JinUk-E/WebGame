// 모바일 브라우저 판별 (Unity WebGL → JS 브릿지)
// 왜 JS인가: Input.touchSupported 는 터치 지원 노트북(마우스 병행)에서도 true라
// 데스크톱 경험을 망가뜨린다. CSS 미디어 쿼리 (pointer: coarse) + (hover: none) 은
// "주 입력이 손가락이고 호버가 없는 기기" = 폰·태블릿만 정확히 걸러낸다.
// (데스크톱 UA를 보내는 iPadOS Safari도 이 조합으로 올바르게 걸린다.)
var MoraeTouchLib = {
  MoraeIsCoarsePointer: function () {
    try {
      if (typeof window === 'undefined') return 0;
      var mm = window.matchMedia;
      if (mm) {
        var coarse = mm('(pointer: coarse)').matches;
        var noHover = mm('(hover: none)').matches;
        if (coarse && noHover) return 1;
        // 매체 질의를 신뢰할 수 없는 구형 브라우저용 보조 판정
        if (!mm('(pointer: fine)').matches && (navigator.maxTouchPoints | 0) > 0) return 1;
        return 0;
      }
      var touch = ('ontouchstart' in window) || (navigator.maxTouchPoints | 0) > 0;
      return (touch && /Mobi|Android|iPhone|iPad|iPod/i.test(navigator.userAgent)) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },
};

mergeInto(LibraryManager.library, MoraeTouchLib);
