using Brainova.BLL.Services.Classes;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// FileService writes uploads to {ContentRoot}/App_Data/{folder}.
    /// These tests use a real temp folder (TempContentRoot) instead of a mock
    /// file system, so they prove the bytes really land on disk.
    /// </summary>
    public class FileServiceTests : IDisposable
    {
        private readonly TempContentRoot _root = new();
        private readonly FileService _sut;

        public FileServiceTests()
        {
            _sut = new FileService(_root.Env.Object);
        }

        // xUnit calls Dispose after each test → the temp folder is removed.
        public void Dispose() => _root.Dispose();

        [Fact]
        public async Task SaveAsync_WhenFileIsNull_Throws()
        {
            var act = () => _sut.SaveAsync(null!, "mri");

            await act.Should().ThrowAsync<Exception>().WithMessage("File is required.");
        }

        [Fact]
        public async Task SaveAsync_WhenFileIsEmpty_Throws()
        {
            var empty = MakeFile("scan.png", Array.Empty<byte>());

            var act = () => _sut.SaveAsync(empty, "mri");

            await act.Should().ThrowAsync<Exception>().WithMessage("File is required.");
        }

        [Fact]
        public async Task SaveAsync_WritesTheBytesIntoAppDataFolder()
        {
            var content = new byte[] { 1, 2, 3, 4, 5 };

            var storedName = await _sut.SaveAsync(MakeFile("scan.png", content), "mri");

            var fullPath = Path.Combine(_root.AppDataFolder("mri"), storedName);
            File.Exists(fullPath).Should().BeTrue();
            File.ReadAllBytes(fullPath).Should().Equal(content);
        }

        [Fact]
        public async Task SaveAsync_StoresUnderANewGuidName_KeepingTheExtension()
        {
            var storedName = await _sut.SaveAsync(MakeFile("My Brain Scan.PNG", new byte[] { 1 }), "mri");

            // 32 hex chars (Guid "N" format) + the original extension.
            storedName.Should().MatchRegex("^[0-9a-f]{32}\\.PNG$");
        }

        [Fact]
        public async Task SaveAsync_WhenFileHasNoExtension_DefaultsToJpg()
        {
            var storedName = await _sut.SaveAsync(MakeFile("scan", new byte[] { 1 }), "mri");

            storedName.Should().EndWith(".jpg");
        }

        [Fact]
        public async Task SaveAsync_CalledTwice_ProducesDifferentNames()
        {
            var first = await _sut.SaveAsync(MakeFile("a.jpg", new byte[] { 1 }), "mri");
            var second = await _sut.SaveAsync(MakeFile("a.jpg", new byte[] { 1 }), "mri");

            first.Should().NotBe(second);
        }

        // Security: the client controls the uploaded file NAME. Because the
        // service never uses that name on disk (only its extension), a name
        // like "../../evil.png" cannot escape the upload folder.
        [Fact]
        public async Task SaveAsync_WhenFileNameContainsPathTraversal_StaysInsideTheFolder()
        {
            var storedName = await _sut.SaveAsync(MakeFile("../../evil.png", new byte[] { 9 }), "mri");

            storedName.Should().NotContain("..").And.NotContain("/").And.NotContain("\\");
            Directory.GetFiles(_root.AppDataFolder("mri")).Should().ContainSingle();
        }

        [Fact]
        public void BuildPath_CombinesContentRootAppDataFolderAndFileName()
        {
            var path = _sut.BuildPath("gradcam", "abc.jpg");

            path.Should().Be(Path.Combine(_root.Root, "App_Data", "gradcam", "abc.jpg"));
        }

        // ---------------------------------------------------------------------
        // Helper: FormFile is ASP.NET's real IFormFile implementation, built
        // here over an in-memory stream instead of an HTTP upload.
        // ---------------------------------------------------------------------
        private static IFormFile MakeFile(string fileName, byte[] content)
        {
            var stream = new MemoryStream(content);
            return new FormFile(stream, 0, content.Length, "file", fileName);
        }
    }
}
