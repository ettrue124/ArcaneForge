using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

// Unity accepts this installation's cross-version references. .NET's default
// loader requires an explicit resolution hook for the same local assemblies.
static class ClientAssemblyResolver
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal) {
        "Wizards.Arena.Enums", "Wizards.Arena.Models", "Wizards.Arena.Models.Protobuf",
        "Wizards.MDN.GreProtobuf", "Google.Protobuf", "Newtonsoft.Json"
    };
    [ModuleInitializer]
    internal static void Register()
    {
        AssemblyLoadContext.Default.Resolving += (context, name) => {
            if (name.Name is null || !Allowed.Contains(name.Name)) return null;
            var loaded = context.Assemblies.FirstOrDefault(a => a.GetName().Name == name.Name);
            if (loaded is not null) return loaded;
            string path = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
    }
}
