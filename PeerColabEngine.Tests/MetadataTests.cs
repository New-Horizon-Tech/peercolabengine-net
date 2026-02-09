using System;
using System.Linq;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class MetadataTests
    {
        [Fact]
        public void Metavalues_DefaultConstructor_HasEmptyCollections()
        {
            var mv = new Metavalues();

            Assert.False(mv.HasMoreValues);
            Assert.Empty(mv.Values);
            Assert.Null(mv.TotalValueCount);
            Assert.Empty(mv.Attributes);
        }

        [Fact]
        public void Metavalues_SetHasMoreValues_SetsFlag()
        {
            var mv = new Metavalues().SetHasMoreValues(true);
            Assert.True(mv.HasMoreValues);
        }

        [Fact]
        public void Metavalues_SetHasMoreValues_DefaultsToTrue()
        {
            var mv = new Metavalues().SetHasMoreValues();
            Assert.True(mv.HasMoreValues);
        }

        [Fact]
        public void Metavalues_SetHasMoreValues_CanSetFalse()
        {
            var mv = new Metavalues().SetHasMoreValues(false);
            Assert.False(mv.HasMoreValues);
        }

        [Fact]
        public void Metavalues_SetTotalValueCount_SetsCount()
        {
            var mv = new Metavalues().SetTotalValueCount(100);
            Assert.Equal(100, mv.TotalValueCount);
        }

        [Fact]
        public void Metavalues_SetTotalValueCount_Null_ClearsCount()
        {
            var mv = new Metavalues().SetTotalValueCount(100).SetTotalValueCount(null);
            Assert.Null(mv.TotalValueCount);
        }

        [Fact]
        public void Metavalues_Add_SingleValue()
        {
            var mv = new Metavalues().Add(new Metavalue { ValueId = "v1" });
            Assert.Single(mv.Values);
            Assert.Equal("v1", mv.Values[0].ValueId);
        }

        [Fact]
        public void Metavalues_Add_MultipleValues()
        {
            var values = new[] {
                new Metavalue { ValueId = "v1" },
                new Metavalue { ValueId = "v2" },
                new Metavalue { ValueId = "v3" }
            };
            var mv = new Metavalues().Add(values);
            Assert.Equal(3, mv.Values.Count);
        }

        [Fact]
        public void Metavalues_HasMetaValue_ReturnsCorrectly()
        {
            var mv = new Metavalues().Add(new Metavalue { ValueId = "v1" });

            Assert.True(mv.HasMetaValue("v1"));
            Assert.False(mv.HasMetaValue("v2"));
        }

        [Fact]
        public void Metavalues_GetMetaValue_ReturnsCorrectValue()
        {
            var mv = new Metavalues()
                .Add(new Metavalue { ValueId = "v1", DataTenant = "t1" })
                .Add(new Metavalue { ValueId = "v2", DataTenant = "t2" });

            var found = mv.GetMetaValue("v2");
            Assert.NotNull(found);
            Assert.Equal("t2", found.DataTenant);
        }

        [Fact]
        public void Metavalues_GetMetaValue_ReturnsNullForMissing()
        {
            var mv = new Metavalues();
            Assert.Null(mv.GetMetaValue("nonexistent"));
        }

        [Fact]
        public void Metavalues_WithAttribute_AddsAttribute()
        {
            var mv = new Metavalues().WithAttribute("key", "value");

            Assert.True(mv.HasAttribute("key"));
            Assert.Equal("value", mv.GetAttribute<string>("key"));
        }

        [Fact]
        public void Metavalues_WithAttribute_UpdatesExisting()
        {
            var mv = new Metavalues()
                .WithAttribute("key", "old")
                .WithAttribute("key", "new");

            Assert.Equal("new", mv.GetAttribute<string>("key"));
            Assert.Single(mv.Attributes);
        }

        [Fact]
        public void Metavalues_HasAttribute_ReturnsFalseForMissing()
        {
            var mv = new Metavalues();
            Assert.False(mv.HasAttribute("missing"));
        }

        [Fact]
        public void Metavalues_GetAttribute_ReturnsDefaultForMissing()
        {
            var mv = new Metavalues();
            Assert.Null(mv.GetAttribute<string>("missing"));
        }

        [Fact]
        public void Metavalues_FluentChaining()
        {
            var mv = new Metavalues()
                .SetHasMoreValues(true)
                .SetTotalValueCount(50)
                .Add(new Metavalue { ValueId = "v1" })
                .WithAttribute("page", 1);

            Assert.True(mv.HasMoreValues);
            Assert.Equal(50, mv.TotalValueCount);
            Assert.Single(mv.Values);
            Assert.True(mv.HasAttribute("page"));
        }

        // Metavalue tests
        [Fact]
        public void Metavalue_WithInitialCharacters_SetsCharacters()
        {
            var chars = CharacterMetaValues.FromPerformer("user", "1");
            var mv = new Metavalue().WithInitialCharacters(chars);

            Assert.True(mv.KnowsInitialCharacters());
            Assert.False(mv.KnowsCurrentCharacters());
            Assert.Equal("user", mv.InitialCharacters.Performer.Type);
        }

        [Fact]
        public void Metavalue_WithCurrentCharacters_SetsCharacters()
        {
            var chars = CharacterMetaValues.FromPerformer("admin", "2");
            var mv = new Metavalue().WithCurrentCharacters(chars);

            Assert.False(mv.KnowsInitialCharacters());
            Assert.True(mv.KnowsCurrentCharacters());
        }

        [Fact]
        public void Metavalue_WithAttribute_AddsAttribute()
        {
            var mv = new Metavalue { ValueId = "v1" };
            mv.WithAttribute("color", "red");

            Assert.True(mv.HasAttribute("color"));
            Assert.Equal("red", mv.GetAttribute<string>("color"));
        }

        [Fact]
        public void Metavalue_WithAttribute_UpdatesExisting()
        {
            var mv = new Metavalue { ValueId = "v1" };
            mv.WithAttribute("color", "red");
            mv.WithAttribute("color", "blue");

            Assert.Equal("blue", mv.GetAttribute<string>("color"));
            Assert.Single(mv.Attributes);
        }

        [Fact]
        public void Metavalue_GetAttribute_ReturnsDefaultForMissing()
        {
            var mv = new Metavalue();
            Assert.Null(mv.GetAttribute<string>("missing"));
        }

        [Fact]
        public void Metavalue_StaticWithAttribute_CreatesWithAttribute()
        {
            var mv = Metavalue.WithAttribute("v1", "status", "active");

            Assert.Equal("v1", mv.ValueId);
            Assert.True(mv.HasAttribute("status"));
            Assert.Equal("active", mv.GetAttribute<string>("status"));
        }

        [Fact]
        public void Metavalue_StaticWith_CreatesComplete()
        {
            var now = DateTime.UtcNow;
            var performer = new Identifier("user", "1");
            var updater = new Identifier("admin", "2");

            var mv = Metavalue.With("v1", "tenant1", performer, now, updater, now);

            Assert.Equal("v1", mv.ValueId);
            Assert.Equal("tenant1", mv.DataTenant);
            Assert.True(mv.KnowsInitialCharacters());
            Assert.True(mv.KnowsCurrentCharacters());
            Assert.Equal("user", mv.InitialCharacters.Performer.Type);
            Assert.Equal("admin", mv.CurrentCharacters.Performer.Type);
        }
    }
}
