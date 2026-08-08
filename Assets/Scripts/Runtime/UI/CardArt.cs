using UnityEngine;

namespace CrossAccel.UI
{
    /// <summary>
    /// docs/CARD_UI_FIX_SPEC.md 좌표표(실측 재확정판)를 코드로 옮긴 것 (1000×1528 원본 기준).
    /// 표시 크기가 달라도 <see cref="Scale"/>만 곱하면 되도록 전부 원본 px로 둔다.
    /// 이전 판(docs/CARD_ASSET_SPEC.md)은 이름/숫자 좌표가 어긋나 있어 CARD_UI_FIX_SPEC.md로 대체됐다.
    /// </summary>
    public static class CardArt
    {
        public const float SourceWidth = 1000f;
        public const float SourceHeight = 1528f;

        /// <summary>에셋 실제 비율 (0.6545). UNITY_BATTLE_UI_SPEC 7번의 0.6494는 에셋과 어긋나 이 값을 쓴다.</summary>
        public const float AspectRatio = SourceWidth / SourceHeight;

        public const string FramePath = "Art/Frames/card_frame_character";
        public const string IllustrationFolder = "Art/Illustrations/";
        public const string FontPath = "Art/Fonts/KOHINanum_Bold SDF";

        /// <summary>원본 px 기준 사각형 (left, top, width, height).</summary>
        public readonly struct Box
        {
            public readonly float Left, Top, Width, Height;
            public Box(float left, float top, float width, float height)
            {
                Left = left; Top = top; Width = width; Height = height;
            }
        }

        // --- 하단 스크림 (CARD_UI_FIX_SPEC.md 반영 — 이름이 y955로 올라오면서 재조정) ---
        /// <summary>
        /// 스크림 시작 y. [출처] CARD_UI_FIX_SPEC.md 좌표표 반영값 — 문서 자체엔 스크림 좌표가 없지만,
        /// 문서가 이름을 y955(Name 박스 상단 910)로 올리면서 "이름 뒤가 안 어둡다"는 문제가 생겨
        /// 사용자 지시("CardArt.ScrimTop 을 이름이 가려지게 위로 당겨도 된다")에 따라 조정했다.
        /// [계산] Name 박스 상단(910)보다 30px 위(880)에서 스크림을 시작해 그라데이션이
        /// 이름 텍스트 시작 전에 완전히 어두워지도록 한다 (기존 983 → 880).
        /// </summary>
        public const float ScrimTop = 880f;

        /// <summary>스크림 사각형: 시작 y부터 카드 하단까지 카드 전폭.</summary>
        public static readonly Box BottomScrim = new Box(0, ScrimTop, SourceWidth, SourceHeight - ScrimTop);

        /// <summary>스크림 색 (위 투명 → 아래 이 색 * ScrimMaxAlpha).</summary>
        public static readonly Color32 ScrimColor = new Color32(10, 14, 24, 255);
        public const float ScrimMaxAlpha = 0.85f;

        /// <summary>
        /// 스크림이 최대 농도에 도달하는 지점 (스크림 높이 대비 비율).
        /// [계산] ScrimTop=880, 스크림 높이=648. Name 박스 상단(y910)에서 이미 최대 농도이길 원해
        /// (910-880)/648 ≈ 0.046 이 필요 → 5px 여유를 두고 0.04로 설정
        /// (880 + 0.04×648 ≈ y905.9 에서 최대 농도 도달, 이름 시작 전).
        /// 기존 0.30(y983 기준 조건/효과용)에서 변경 — 이름이 스크림 최상단에 훨씬 가까워졌기 때문.
        /// </summary>
        public const float ScrimRampEnd = 0.04f;

        // --- 좌표 (CARD_UI_FIX_SPEC.md 좌표표, 1000×1528 기준) ---
        // 체력/리치: 문서 위치(147,150)/(852,150)는 기존 실측 hex 크기(182x110/180x110)와
        // "사실상 일치"(사용자 확인)하여 hex 크기는 유지하고 중심좌표만 문서값에 맞춰 재계산했다.
        public static readonly Box Hp = new Box(56, 95, 182, 110);       // 중심 (147,150)
        public static readonly Box Reach = new Box(762, 95, 180, 110);   // 중심 (852,150)

        // 이름: 문서 중심(500,955). 폭은 문서에 없어 조건/효과와 같은 좌우 여백(195)에 맞춰 610으로 잡았다(임의).
        public static readonly Box Name = new Box(195, 910, 610, 90);    // 중심 (500,955)

        // 직업태그(무기타입)=x430, 종족태그=x560 — 문서가 지정한 대로 좌우를 나눴다(기존 코드는 반대였음).
        // 크기(114x54)는 문서에 없어 기존 실측값 유지(임의).
        public static readonly Box WeaponTag = new Box(373, 1013, 114, 54); // 중심 (430,1040) — 직업태그
        public static readonly Box RaceTag = new Box(503, 1013, 114, 54);  // 중심 (560,1040) — 종족태그

        // 조건/효과: 문서가 준 좌표는 "시작점"(좌상단)이라 그대로 Left,Top으로 쓴다.
        // 폭 610은 이름과 동일한 좌우 여백(195)을 맞춘 값(임의). 효과 높이 150은
        // 문서에 없어 하단 종족/직업 행(top 1375)과 안 겹치게 역산했다(임의).
        public static readonly Box Condition = new Box(195, 1110, 610, 56);
        public static readonly Box Effect = new Box(195, 1210, 610, 150);

        // 하단 종족/직업(이름이 955로 올라가며 비게 된 자리를 채우는 새 요소). 폭 500은 임의.
        public static readonly Box Footer = new Box(250, 1375, 500, 50);  // 중심 (500,1400)

        // --- 폰트 크기 (CARD_UI_FIX_SPEC.md 좌표표) ---
        public const float StatFontSize = 60f;       // 문서: 60 (기존 96에서 축소)
        public const float TagFontSize = 30f;
        public const float ConditionFontSize = 30f;
        public const float EffectFontSize = 30f;
        public const float NameFontSize = 50f;       // 문서: 50 (기존 52에서 축소)
        public const float FooterFontSize = 34f;     // 문서: 34

        // --- 색 (CARD_UI_FIX_SPEC.md "발견된 버그와 원인" 표: 이름·숫자 흰색 #EEF3FA, 조건/효과 밝은회 #DFE7F2) ---
        public static readonly Color StatColor = new Color32(0xEE, 0xF3, 0xFA, 0xFF);
        public static readonly Color NameColor = new Color32(0xEE, 0xF3, 0xFA, 0xFF);
        public static readonly Color ConditionColor = new Color32(0xDF, 0xE7, 0xF2, 0xFF);
        public static readonly Color EffectColor = new Color32(0xDF, 0xE7, 0xF2, 0xFF);

        // 연보라(태그) — 문서에 "연보라"라고만 적혀 있고 hex가 없어 기존 값 유지(임의).
        public static readonly Color TagColor = new Color32(0xC9, 0xB8, 0xF0, 0xFF);

        // 회색(하단 종족/직업) — 문서에 "회색"이라고만 적혀 있고 hex가 없어 조건/효과의 밝은회(0xDFE7F2)보다
        // 어둡게 골랐다(임의) — 이름 바로 아래 보조 정보라 주 텍스트보다 눈에 덜 띄어야 하기 때문.
        public static readonly Color FooterColor = new Color32(0x8A, 0x93, 0xA6, 0xFF);

        /// <summary>표시 폭에 대한 배율. 이 값을 모든 좌표·폰트에 곱한다.</summary>
        public static float Scale(float displayWidth) => displayWidth / SourceWidth;

        /// <summary>표시 폭에 맞는 카드 높이 (에셋 비율 유지).</summary>
        public static float HeightFor(float displayWidth) => displayWidth / AspectRatio;
    }
}
