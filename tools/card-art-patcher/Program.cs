using Mono.Cecil;
using Mono.Cecil.Cil;

if(args.Length is 4 or 5 && args[0]=="--loyalty-display") {
    var loyaltyResolver=new DefaultAssemblyResolver();
    loyaltyResolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    if(args.Length==5)loyaltyResolver.AddSearchDirectory(Path.GetFullPath(args[4]));
    using var target=ModuleDefinition.ReadModule(args[1],new ReaderParameters {AssemblyResolver=loyaltyResolver});
    using var support=ModuleDefinition.ReadModule(args[2]);
    var getter=target.Types.Single(t=>t.FullName=="GreClient.CardData.AbilityPrintingData").Methods.Single(m=>m.Name=="get_LoyaltyCost");
    if(!getter.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="ArcaneForge.LoyaltyDisplay")) {
        var data=getter.DeclaringType;
        var getId=target.ImportReference(data.Methods.Single(m=>m.Name=="get_Id"));
        var dynamicType=target.Types.Single(t=>t.FullName=="GreClient.CardData.DynamicAbilityPrintingData");
        var cost=target.ImportReference(support.Types.Single(t=>t.FullName=="ArcaneForge.LoyaltyDisplay").Methods.Single(m=>m.Name=="Cost"));
        var constructor=target.Types.Single(t=>t.FullName=="StringBackedInt").Methods.Single(m=>m.IsConstructor && m.Parameters.Count==1 && m.Parameters[0].ParameterType.MetadataType==MetadataType.String);
        var il=getter.Body.GetILProcessor();var original=getter.Body.Instructions[0];
        foreach(var instruction in new[]{il.Create(OpCodes.Ldarg_0),il.Create(OpCodes.Isinst,dynamicType),il.Create(OpCodes.Brfalse,original),
            il.Create(OpCodes.Ldarg_0),il.Create(OpCodes.Call,getId),il.Create(OpCodes.Ldc_I4,unchecked((int)0xf0000000)),il.Create(OpCodes.And),
            il.Create(OpCodes.Ldc_I4,0x70000000),il.Create(OpCodes.Bne_Un,original),
            il.Create(OpCodes.Ldarg_0),il.Create(OpCodes.Call,getId),il.Create(OpCodes.Call,cost),il.Create(OpCodes.Newobj,constructor),il.Create(OpCodes.Ret)})il.InsertBefore(original,instruction);
    }
    target.Write(args[3]);Console.WriteLine("Dynamic loyalty cost preview prepared.");return;
}

if(args.Length is 4 or 5 && (args[0]=="--validation" || args[0]=="--validation-client")) {
    var validationResolver=new DefaultAssemblyResolver();validationResolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    if(args.Length==5)validationResolver.AddSearchDirectory(Path.GetFullPath(args[4]));
    using var validation=ModuleDefinition.ReadModule(args[1],new ReaderParameters {AssemblyResolver=validationResolver});
    using var support=ModuleDefinition.ReadModule(args[2]);
    var hooks=support.Types.Single(t=>t.FullName=="ArcaneForge.RulebreakerDecks");
    if(args[0]=="--validation-client") {
        var target=validation.Types.Single(t=>t.FullName=="Wizards.Arena.DeckValidation.Client.DeckValidation").Methods.Single(m=>m.Name=="ClientResultFromContext");
        if(!target.Body.Instructions.Any(i=>i.Operand is MethodReference called && called.DeclaringType.FullName==hooks.FullName)) {
            var il=target.Body.GetILProcessor();
            var hook=validation.ImportReference(hooks.Methods.Single(m=>m.Name=="AdjustValidationResult"));
            foreach(var ret in target.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
                il.InsertBefore(ret,il.Create(OpCodes.Dup));il.InsertBefore(ret,il.Create(OpCodes.Ldarg_0));il.InsertBefore(ret,il.Create(OpCodes.Call,hook));
            }
        }
        validation.Write(args[3]);Console.WriteLine("Rulebreaker deck size display prepared.");return;
    }
    foreach(var pair in new[]{("TitleMatchesCommanderColors","ExemptsTitle"),("DeckPileSizeCheck","AllowsMainSize")}) {
        var target=validation.Types.Single(t=>t.Name==pair.Item1).Methods.Single(m=>m.Name=="Validate");
        if(target.Body.Instructions.Any(i=>i.Operand is MethodReference called && called.DeclaringType.FullName==hooks.FullName))continue;
        var hook=validation.ImportReference(hooks.Methods.Single(m=>m.Name==pair.Item2));
        var il=target.Body.GetILProcessor();var original=target.Body.Instructions[0];
        var prefix=new List<Instruction>{il.Create(OpCodes.Ldarg_1),il.Create(OpCodes.Ldarg_2)};
        if(target.Parameters[1].ParameterType.IsValueType)
            prefix.Add(il.Create(OpCodes.Box,target.Parameters[1].ParameterType));
        prefix.AddRange(new[]{il.Create(OpCodes.Call,hook),il.Create(OpCodes.Brfalse,original),il.Create(OpCodes.Ldc_I4_1),il.Create(OpCodes.Ret)});
        foreach(var instruction in prefix)il.InsertBefore(original,instruction);
    }
    var sanity=validation.Types.Single(t=>t.Name=="DeckDataSizeSanityCheck").Methods.Single(m=>m.Name=="Validate");
    if(!sanity.Body.Instructions.Any(i=>i.Operand is MethodReference called && called.DeclaringType.FullName==hooks.FullName)) {
        var limit=sanity.Body.Variables.First(v=>v.VariableType.MetadataType==MetadataType.Int32);
        var store=sanity.Body.Instructions.First(i=>i.OpCode==OpCodes.Stloc && i.Operand==limit || i.OpCode==OpCodes.Stloc_S && i.Operand==limit
            || i.OpCode==OpCodes.Stloc_0 && limit.Index==0 || i.OpCode==OpCodes.Stloc_1 && limit.Index==1
            || i.OpCode==OpCodes.Stloc_2 && limit.Index==2 || i.OpCode==OpCodes.Stloc_3 && limit.Index==3);
        var il=sanity.Body.GetILProcessor();il.InsertBefore(store,il.Create(OpCodes.Ldarg_1));
        il.InsertBefore(store,il.Create(OpCodes.Call,validation.ImportReference(hooks.Methods.Single(m=>m.Name=="SanityMaximum"))));
    }
    validation.Write(args[3]);Console.WriteLine("Commander Rulebreaker validation hooks prepared.");return;
}
if(args.Length!=3)throw new ArgumentException("Input Core.dll, helper DLL, output Core.dll required");
var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
using var module=ModuleDefinition.ReadModule(args[0],new ReaderParameters {AssemblyResolver=resolver});
using var helper=ModuleDefinition.ReadModule(args[1]);
IEnumerable<TypeDefinition> AllTypes(TypeDefinition type) {yield return type;foreach(var nested in type.NestedTypes)foreach(var child in AllTypes(nested))yield return child;}
var ruleMatches=module.ImportReference(helper.Types.Single(t=>t.FullName=="ArcaneForge.RulebreakerDecks").Methods.Single(m=>m.Name=="CardMatches"));
var builder=module.Types.Single(t=>t.FullName=="Core.Code.Decks.DeckBuilderCardFilterProvider");
int ruleHooks=0;
foreach(var target in AllTypes(builder).SelectMany(t=>t.Methods).Where(m=>m.HasBody)) {
    foreach(var instruction in target.Body.Instructions.ToArray()) {
        if(instruction.Operand is not MethodReference call || call.Name!="CardMatchesCommanderColors")continue;
        if(!target.HasThis)throw new InvalidOperationException("Commander filter closure changed.");
        target.Body.GetILProcessor().InsertBefore(instruction,Instruction.Create(OpCodes.Ldarg_0));
        instruction.OpCode=OpCodes.Call;instruction.Operand=ruleMatches;ruleHooks++;
    }
}
if(ruleHooks==0 && !AllTypes(builder).SelectMany(t=>t.Methods).Where(m=>m.HasBody).Any(m=>m.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.DeclaringType.FullName=="ArcaneForge.RulebreakerDecks")))
    throw new InvalidOperationException("Commander color filter not found.");
var rewriteSearch=module.ImportReference(helper.Types.Single(t=>t.FullName=="ArcaneForge.PlaytestSearch").Methods.Single(m=>m.Name=="Rewrite"));
var titleFilter=module.Types.Single(t=>t.FullName=="StringFilter").Methods.Single(m=>m.Name=="ValueMatch_Title");
if(!titleFilter.Body.Instructions.Any(i=>i.Operand is MethodReference called && called.DeclaringType.FullName=="ArcaneForge.PlaytestSearch")) {
    var apply=module.ImportReference(helper.Types.Single(t=>t.FullName=="ArcaneForge.PlaytestSearch").Methods.Single(m=>m.Name=="ApplyTitleFilter"));
    var il=titleFilter.Body.GetILProcessor();var original=titleFilter.Body.Instructions[0];
    foreach(var instruction in new[]{il.Create(OpCodes.Ldarg_0),il.Create(OpCodes.Ldarg_1),il.Create(OpCodes.Call,apply),
        il.Create(OpCodes.Brfalse,original),il.Create(OpCodes.Ldarg_1),il.Create(OpCodes.Ret)})il.InsertBefore(original,instruction);
}
foreach(var constructor in module.Types.Single(t=>t.FullName=="CardMatcher").Methods.Where(m=>m.IsConstructor && !m.IsStatic && m.Parameters.Count>0 && m.Parameters[0].ParameterType.FullName=="System.String")) {
    if(constructor.Body.Instructions.Any(i=>i.Operand is MethodReference called && called.DeclaringType.FullName=="ArcaneForge.PlaytestSearch"))continue;
    var il=constructor.Body.GetILProcessor();var original=constructor.Body.Instructions[0];
    foreach(var instruction in new[]{il.Create(OpCodes.Ldarg,constructor.Parameters[0]),il.Create(OpCodes.Call,rewriteSearch),il.Create(OpCodes.Starg,constructor.Parameters[0])})il.InsertBefore(original,instruction);
}
// Long costs must fit the width reserved by CDCFieldFiller for the mana bar.
// The native 2.5 minimum can overflow that reserved width with ten symbols.
var manaUpdate=module.Types.Single(t=>t.FullName=="CDCManaCostFiller").Methods.Single(m=>m.Name=="UpdateField");
int manaFitChanges=0;
foreach(var instruction in manaUpdate.Body.Instructions) {
    if(instruction.OpCode==OpCodes.Ldc_R4 && instruction.Operand is float size && size==2.5f
        && instruction.Next?.Operand is MethodReference setter && setter.Name=="set_fontSizeMin") {
        instruction.Operand=.75f;manaFitChanges++;
    }
}
if(manaFitChanges==0 && !manaUpdate.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldc_R4 && i.Operand is float value && value==.75f
    && i.Next?.Operand is MethodReference setter && setter.Name=="set_fontSizeMin"))
    throw new InvalidOperationException("Native mana sizing pattern changed; refusing to patch.");
Console.WriteLine("Long mana costs fit within the title bar.");
var method=module.Types.Single(t=>t.FullName=="CardArtTextureLoader").Methods.Single(m=>m.Name=="AcquireCardArt");
if(method.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="ArcaneForge.LocalCardArt")) {
    Console.WriteLine("Direct art lookup already present.");
} else {
    var directLoad=module.ImportReference(helper.Types.Single(t=>t.FullName=="ArcaneForge.LocalCardArt").Methods.Single(m=>m.Name=="Load"));
    var directIl=method.Body.GetILProcessor();var directOriginal=method.Body.Instructions[0];
    var directTexture=new VariableDefinition(method.ReturnType);method.Body.Variables.Add(directTexture);method.Body.InitLocals=true;
    foreach(var instruction in new[]{directIl.Create(OpCodes.Ldarg_3),directIl.Create(OpCodes.Call,directLoad),directIl.Create(OpCodes.Stloc,directTexture),
        directIl.Create(OpCodes.Ldloc,directTexture),directIl.Create(OpCodes.Brfalse,directOriginal),directIl.Create(OpCodes.Ldloc,directTexture),directIl.Create(OpCodes.Ret)})
        directIl.InsertBefore(directOriginal,instruction);
}
var load=module.ImportReference(helper.Types.Single(t=>t.FullName=="ArcaneForge.LocalCardArt").Methods.Single(m=>m.Name=="Load"));
var has=module.ImportReference(helper.Types.Single(t=>t.FullName=="ArcaneForge.LocalCardArt").Methods.Single(m=>m.Name=="Has"));
var artPath=module.Types.Single(t=>t.FullName=="CardArtTextureLoader").Methods.Single(m=>m.Name=="GetCardArtPath");
var asset=module.Types.Single(t=>t.FullName=="AssetLoader");
var acquire=asset.Methods.Single(m=>m.Name=="AcquireAsset" && m.HasGenericParameters);
foreach(var entry in new[]{artPath,acquire,asset.Methods.Single(m=>m.Name=="HaveAsset" && !m.HasGenericParameters),asset.Methods.Single(m=>m.Name=="ReleaseAsset")}) {
    if(entry.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="ArcaneForge.LocalCardArt"))continue;
    var il=entry.Body.GetILProcessor();var original=entry.Body.Instructions[0];
    void Insert(params Instruction[] instructions) {foreach(var instruction in instructions)il.InsertBefore(original,instruction);}
    if(entry==acquire) {
        var texture=new VariableDefinition(load.ReturnType);entry.Body.Variables.Add(texture);entry.Body.InitLocals=true;
        Insert(il.Create(OpCodes.Ldarg_0),il.Create(OpCodes.Call,load),il.Create(OpCodes.Stloc,texture),
            il.Create(OpCodes.Ldloc,texture),il.Create(OpCodes.Brfalse,original),il.Create(OpCodes.Ldloc,texture),
            il.Create(OpCodes.Castclass,entry.GenericParameters[0]),il.Create(OpCodes.Ret));
    } else {
        Insert(il.Create(entry.IsStatic?OpCodes.Ldarg_0:OpCodes.Ldarg_1),il.Create(OpCodes.Call,has),il.Create(OpCodes.Brfalse,original));
        if(entry==artPath)Insert(il.Create(OpCodes.Ldarg_1));
        else if(entry.Name=="HaveAsset")Insert(il.Create(OpCodes.Ldc_I4_1));
        Insert(il.Create(OpCodes.Ret));
    }
}
module.Write(args[2]);
Console.WriteLine("Added local artwork lookup before the original card-art loader.");
