using System.Reflection;
using System.Runtime.Loader;
using Google.Protobuf;
using Google.Protobuf.Reflection;

if(args.Length!=2)throw new ArgumentException("Expected managed directory and output directory");
string managed=Path.GetFullPath(args[0]), output=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving+=(_,name)=> {
    string path=Path.Combine(managed,name.Name+".dll");
    return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null;
};
Extractor.Export(managed,output);

static class Extractor
{
    public static void Export(string managed,string output)
    {
        Directory.CreateDirectory(output);
        var files=new Dictionary<string,FileDescriptor>();
        void Visit(FileDescriptor file) {
            if(files.TryGetValue(file.Name,out var existing)) {
                if(!existing.SerializedData.Equals(file.SerializedData))throw new InvalidDataException("Conflicting descriptor: "+file.Name);
                return;
            }
            files.Add(file.Name,file);
            foreach(var dependency in file.Dependencies)Visit(dependency);
        }
        // Load descriptors from the requested client, rather than the private
        // server's copied assembly references. Never initialize Unity/Core.
        foreach(string name in new[]{"Wizards.MDN.GreProtobuf","Wizards.Arena.Models.Protobuf"}) {
            var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed,name+".dll"));
            foreach(var type in assembly.GetExportedTypes()) {
                var descriptor=type.GetProperty("Descriptor",BindingFlags.Public|BindingFlags.Static);
                if(descriptor?.PropertyType==typeof(MessageDescriptor))Visit(((MessageDescriptor)descriptor.GetValue(null)!).File);
                else if(descriptor?.PropertyType==typeof(FileDescriptor))Visit((FileDescriptor)descriptor.GetValue(null)!);
            }
        }
        if(files.Count==0)throw new InvalidDataException("No client protobuf descriptors found");
        var set=new FileDescriptorSet();
        foreach(var file in files.Values.OrderBy(f=>f.Name))set.File.Add(FileDescriptorProto.Parser.ParseFrom(file.SerializedData));
        File.WriteAllBytes(Path.Combine(output,"arena-descriptor-set.pb"),set.ToByteArray());
        File.WriteAllText(Path.Combine(output,"arena-descriptor-set.json"),JsonFormatter.Default.Format(set));
        Console.WriteLine($"Extracted {files.Count} protocol files from the selected client DLLs.");
    }
}
