# -*- coding: utf-8 -*-
"""밀실 버티기 — 스프라이트 전량 절차 생성 진입점.

사용법:  python generate_all.py        (Tools/art-gen/ 에서 실행)
필요:    pip install pillow
출력:    Assets/_Project/Art/{Room,Props,UI,Portraits}/*.png + Tools/art-gen/preview.png

모든 난수 시드는 각 gen_* 모듈에 상수로 고정 — 재실행 시 항상 동일한 PNG.
아트 디렉션 팔레트/톤 상수는 artgen_common.py 참조.
"""
import gen_portraits
import gen_preview
import gen_props
import gen_room
import gen_ui


def main():
    gen_room.run()
    gen_props.run()
    gen_ui.run()
    gen_portraits.run()
    gen_preview.run()
    print("done.")


if __name__ == "__main__":
    main()
