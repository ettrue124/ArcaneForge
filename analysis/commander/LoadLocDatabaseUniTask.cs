using Core.Code.Localization;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Wizards.Mtga;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtga.Loc.CachingPatterns;

namespace Core.Shared.Code.ServiceFactories;

public class LoadLocDatabaseUniTask
{
	public async UniTask Load()
	{
		UnityCrossThreadLogger unityCrossThreadLogger = new UnityCrossThreadLogger();
		if (Pantry.Get<IClientLocProvider>() is CompositeLocProvider { ProviderCount: <2u } compositeLocProvider)
		{
			IBILogger biLogger = Pantry.Get<IBILogger>();
			string rawFilePath = AssetLoader.GetRawFilePath("ClientLocalization", "ClientLocalization.sqlite");
			if (!string.IsNullOrWhiteSpace(rawFilePath) && FileSystemUtils.FileExists(rawFilePath))
			{
				unityCrossThreadLogger.Debug("Loading SqlLocalizationManager from file: " + rawFilePath, new JObject());
				IClientLocProvider newProvider = new SqlLocalizationManager(rawFilePath, biLogger, new DictionaryCache<string, string>(1000), Pantry.Get<ISqlHelper>());
				compositeLocProvider.InsertProvider(newProvider);
			}
		}
	}
}
