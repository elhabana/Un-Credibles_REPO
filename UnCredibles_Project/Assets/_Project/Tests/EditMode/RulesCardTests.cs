using NUnit.Framework;
using UnCredibles.Minigames;

namespace UnCredibles.Tests
{
    // "Move: drive · Jump: turbo" lines of the mechanic pages: glyph (lower case) and action (capitalised).
    public sealed class RulesCardTests
    {
        [Test]
        public void SplitsGlyphAndAction()
        {
            Assert.IsTrue(MinigameRulesCard.SplitControl(" Jump: empuja ", out var glyph, out var action));
            Assert.AreEqual("jump", glyph);
            Assert.AreEqual("Empuja", action);
        }

        [Test]
        public void WithoutColonItIsAllAction()
        {
            Assert.IsTrue(MinigameRulesCard.SplitControl("vacía las bolsas", out var glyph, out var action));
            Assert.AreEqual(string.Empty, glyph);
            Assert.AreEqual("Vacía las bolsas", action);
        }

        [Test]
        public void EmptyPartsAreSkipped()
        {
            Assert.IsFalse(MinigameRulesCard.SplitControl("  ", out _, out _));
            Assert.IsFalse(MinigameRulesCard.SplitControl(null, out _, out _));
        }
    }
}
