using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using MockQueryable;
using Moq;

namespace Brainova.Tests.TestSupport
{
    /// <summary>
    /// Shared helpers so every test class does not repeat the same plumbing.
    /// </summary>
    public static class MockExtensions
    {
        /// <summary>
        /// Wires uow.Repo&lt;T&gt;() to a fake repository whose Query() serves
        /// the given in-memory list (async-capable via MockQueryable).
        /// Returns the repo mock so a test can Setup/Verify more calls on it.
        /// Usage: var repo = _uow.SetupRepo(new List&lt;MriCase&gt; { ... });
        /// </summary>
        public static Mock<IGenericRepository<T>> SetupRepo<T>(this Mock<IUnitOfWork> uow, List<T> data)
            where T : class
        {
            var repo = new Mock<IGenericRepository<T>>();
            repo.Setup(r => r.Query()).Returns(data.BuildMock());
            uow.Setup(u => u.Repo<T>()).Returns(repo.Object);
            return repo;
        }
    }

    /// <summary>
    /// ASP.NET Identity classes have no interfaces and long constructors.
    /// These build mocks of them the standard way.
    /// </summary>
    public static class IdentityMocks
    {
        /// <summary>UserManager: only IUserStore is required, the other 8 can be null.</summary>
        public static Mock<UserManager<ApplicationUser>> CreateUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        /// <summary>
        /// SignInManager: needs a UserManager, an IHttpContextAccessor and a
        /// claims factory; everything else can be null. The constructor is
        /// found by reflection so this keeps working if a .NET version adds
        /// a parameter.
        /// </summary>
        public static Mock<SignInManager<ApplicationUser>> CreateSignInManager(
            Mock<UserManager<ApplicationUser>> userManager)
        {
            var ctor = typeof(SignInManager<ApplicationUser>)
                .GetConstructors()
                .OrderBy(c => c.GetParameters().Length)
                .First();

            var args = new List<object?>();
            foreach (var p in ctor.GetParameters())
            {
                if (p.ParameterType == typeof(UserManager<ApplicationUser>))
                    args.Add(userManager.Object);
                else if (p.ParameterType == typeof(IHttpContextAccessor))
                    args.Add(Mock.Of<IHttpContextAccessor>());
                else if (p.ParameterType == typeof(IUserClaimsPrincipalFactory<ApplicationUser>))
                    args.Add(Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>());
                else
                    args.Add(null);
            }

            return new Mock<SignInManager<ApplicationUser>>(args.ToArray()!);
        }
    }

    /// <summary>
    /// A throw-away folder that plays the role of the API's content root
    /// (where App_Data/mri and App_Data/gradcam live). Deleted on Dispose,
    /// so tests that touch the disk leave nothing behind.
    /// </summary>
    public sealed class TempContentRoot : IDisposable
    {
        public string Root { get; }
        public Mock<IWebHostEnvironment> Env { get; } = new();

        public TempContentRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), "brainova-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Env.Setup(e => e.ContentRootPath).Returns(Root);
        }

        /// <summary>Creates App_Data/{folder}/{fileName} with the given bytes and returns its full path.</summary>
        public string WriteAppDataFile(string folder, string fileName, byte[] content)
        {
            var dir = Path.Combine(Root, "App_Data", folder);
            Directory.CreateDirectory(dir);
            var fullPath = Path.Combine(dir, fileName);
            File.WriteAllBytes(fullPath, content);
            return fullPath;
        }

        public string AppDataFolder(string folder) => Path.Combine(Root, "App_Data", folder);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; a leftover temp folder must not fail a test.
            }
        }
    }
}
