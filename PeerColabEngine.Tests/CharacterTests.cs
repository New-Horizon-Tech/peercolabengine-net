using System;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class CharacterTests
    {
        [Fact]
        public void Identifier_Constructor_SetsProperties()
        {
            var id = new Identifier("user", "123");

            Assert.Equal("user", id.Type);
            Assert.Equal("123", id.Id);
        }

        [Fact]
        public void Identifier_DefaultConstructor_HasNullProperties()
        {
            var id = new Identifier();

            Assert.Null(id.Type);
            Assert.Null(id.Id);
        }

        [Fact]
        public void Characters_DefaultConstructor_HasNullIdentifiers()
        {
            var chars = new Characters();

            Assert.Null(chars.Subject);
            Assert.Null(chars.Responsible);
            Assert.Null(chars.Performer);
        }

        [Fact]
        public void Characters_CopyConstructor_CopiesFromInterface()
        {
            var source = new CharacterMetaValues
            {
                Subject = new Identifier("user", "1"),
                Responsible = new Identifier("admin", "2"),
                Performer = new Identifier("system", "3")
            };

            var chars = new Characters(source);

            Assert.Equal("user", chars.Subject.Type);
            Assert.Equal("1", chars.Subject.Id);
            Assert.Equal("admin", chars.Responsible.Type);
            Assert.Equal("system", chars.Performer.Type);
        }

        [Fact]
        public void CharacterMetaValues_FromSubject_SetsSubject()
        {
            var id = new Identifier("user", "42");
            var cmv = CharacterMetaValues.FromSubject(id);

            Assert.True(cmv.HasSubject());
            Assert.Equal("user", cmv.Subject.Type);
            Assert.Equal("42", cmv.Subject.Id);
            Assert.False(cmv.HasResponsible());
            Assert.False(cmv.HasPerformer());
        }

        [Fact]
        public void CharacterMetaValues_FromSubject_StringOverload()
        {
            var cmv = CharacterMetaValues.FromSubject("user", "42");

            Assert.True(cmv.HasSubject());
            Assert.Equal("user", cmv.Subject.Type);
            Assert.Equal("42", cmv.Subject.Id);
        }

        [Fact]
        public void CharacterMetaValues_FromResponsible_SetsResponsible()
        {
            var cmv = CharacterMetaValues.FromResponsible(new Identifier("admin", "1"));

            Assert.True(cmv.HasResponsible());
            Assert.Equal("admin", cmv.Responsible.Type);
        }

        [Fact]
        public void CharacterMetaValues_FromResponsible_StringOverload()
        {
            var cmv = CharacterMetaValues.FromResponsible("admin", "1");

            Assert.True(cmv.HasResponsible());
            Assert.Equal("admin", cmv.Responsible.Type);
        }

        [Fact]
        public void CharacterMetaValues_FromPerformer_SetsPerformer()
        {
            var cmv = CharacterMetaValues.FromPerformer(new Identifier("system", "sys"));

            Assert.True(cmv.HasPerformer());
            Assert.Equal("system", cmv.Performer.Type);
        }

        [Fact]
        public void CharacterMetaValues_FromPerformer_StringOverload()
        {
            var cmv = CharacterMetaValues.FromPerformer("system", "sys");

            Assert.True(cmv.HasPerformer());
            Assert.Equal("system", cmv.Performer.Type);
        }

        [Fact]
        public void CharacterMetaValues_FromTimestamp_SetsTimestamp()
        {
            var now = DateTime.UtcNow;
            var cmv = CharacterMetaValues.FromTimestamp(now);

            Assert.True(cmv.HasTimestamp());
            Assert.Equal(now, cmv.Timestamp);
        }

        [Fact]
        public void CharacterMetaValues_FluentChaining_BuildsComplete()
        {
            var now = DateTime.UtcNow;
            var cmv = CharacterMetaValues.FromSubject("user", "1")
                .WithResponsible("admin", "2")
                .WithPerformer("system", "3")
                .WithTimestamp(now);

            Assert.True(cmv.HasSubject());
            Assert.True(cmv.HasResponsible());
            Assert.True(cmv.HasPerformer());
            Assert.True(cmv.HasTimestamp());
            Assert.Equal("user", cmv.Subject.Type);
            Assert.Equal("admin", cmv.Responsible.Type);
            Assert.Equal("system", cmv.Performer.Type);
            Assert.Equal(now, cmv.Timestamp);
        }

        [Fact]
        public void CharacterMetaValues_WithSubject_IdentifierOverload()
        {
            var cmv = new CharacterMetaValues()
                .WithSubject(new Identifier("user", "x"));

            Assert.Equal("user", cmv.Subject.Type);
            Assert.Equal("x", cmv.Subject.Id);
        }

        [Fact]
        public void CharacterMetaValues_WithResponsible_IdentifierOverload()
        {
            var cmv = new CharacterMetaValues()
                .WithResponsible(new Identifier("admin", "x"));

            Assert.Equal("admin", cmv.Responsible.Type);
        }

        [Fact]
        public void CharacterMetaValues_WithPerformer_IdentifierOverload()
        {
            var cmv = new CharacterMetaValues()
                .WithPerformer(new Identifier("sys", "x"));

            Assert.Equal("sys", cmv.Performer.Type);
        }

        [Fact]
        public void CharacterMetaValues_NullTimestamp()
        {
            var cmv = new CharacterMetaValues().WithTimestamp(null);

            Assert.False(cmv.HasTimestamp());
            Assert.Null(cmv.Timestamp);
        }

        [Fact]
        public void ICharacters_Interface_ImplementedByCharacters()
        {
            ICharacters chars = new Characters();
            Assert.NotNull(chars);
        }

        [Fact]
        public void ICharacters_Interface_ImplementedByCharacterMetaValues()
        {
            ICharacters chars = new CharacterMetaValues();
            Assert.NotNull(chars);
        }
    }
}
