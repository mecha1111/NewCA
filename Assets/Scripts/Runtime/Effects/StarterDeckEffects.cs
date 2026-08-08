using CrossAccel.Battle;

namespace CrossAccel.Effects
{
    /// <summary>
    /// 스타터 덱 2종의 카드 효과를 GameManager에 붙이는 진입점.
    /// 효과 없이 페이즈만 돌리고 싶으면 이걸 호출하지 않으면 된다 (Phase 3까지의 동작).
    /// </summary>
    public static class StarterDeckEffects
    {
        /// <summary>스킬·캐릭터 효과를 등록하고 GameManager에 연결한다. 등록된 EffectSystem을 돌려준다.</summary>
        public static EffectSystem Install(GameManager game)
        {
            var characterEffects = new CharacterEffectSystem { Log = game.Log };
            characterEffects.RegisterStarterDeckCharacters();

            var skillEffects = new EffectSystem(characterEffects) { Log = game.Log };
            skillEffects.RegisterStarterDeckSkills();

            game.CharacterEffects = characterEffects;
            game.SkillEffectResolver = ctx => skillEffects.Execute(ctx);
            return skillEffects;
        }
    }
}
