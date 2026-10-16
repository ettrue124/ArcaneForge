using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;
using Wizards.Arena.Client.Logging;
using Wizards.Mtga;
using Wizards.Mtga.Logging;
using Wizards.Mtga.Utils;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc.CachingPatterns;

namespace Wotc.Mtga.Loc;

public class SqlLocalizationManager : IClientLocProvider, IDisposable
{
	private const string SELECT_TEXT_COMMAND = "SELECT {0} FROM Loc WHERE Key = @key";

	private const string SELECT_ALL_KEYS_COMMAND = "SELECT Key FROM Loc";

	public const int MAX_LOC_KEY_BYTES = 250;

	private SqliteConnection _dbConnection;

	private readonly Dictionary<string, SqliteCommand> _selectCommandsByLangCode = new Dictionary<string, SqliteCommand>(2);

	private readonly ICachingPattern<string, string> _textCache;

	private IReadOnlyCollection<string> _allKeysCache;

	private readonly IBILogger _biLogger;

	private readonly ILogger _logger = new UnityLogger("SqlLocalizationManager", LoggerLevel.Error);

	private readonly StringCache _stringCache = new StringCache();

	private readonly string _connectionString;

	public SqlLocalizationManager(string dbPath, IBILogger biLogger, ICachingPattern<string, string> textCache, ISqlHelper sqlHelper)
	{
		_logger.Info("Creating SqlLocalizationManager");
		_biLogger = biLogger ?? new NullBILogger();
		_textCache = textCache ?? new NullCache<string, string>();
		SqlHelperExtensions.TryInitSql(sqlHelper);
		_connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = dbPath,
			Cache = SqliteCacheMode.Private,
			Mode = SqliteOpenMode.ReadOnly,
			Pooling = false
		}.ToString();
		OpenConnection();
		GetKeys();
		Languages.LanguageChangedSignal.Listeners += OnLanguageChanged;
	}

	public void OpenConnection()
	{
		if (_dbConnection != null)
		{
			CloseConnection();
		}
		_logger.Info("Opening connection to " + _connectionString);
		_dbConnection = new SqliteConnection(_connectionString);
		_dbConnection.Open();
		SqlHelperExtensions.RunReadonlyPragmas(_dbConnection, _biLogger, -2000, exclusive: true);
	}

	private void OnLanguageChanged()
	{
		_textCache.ClearCache();
	}

	public string GetLocalizedText(string key, params (string, string)[] locParams)
	{
		return GetLocalizedTextForLanguage(key, Languages.CurrentLanguage, locParams);
	}

	public bool TryGetLocalizedTextForLanguage(string key, string overrideLangCode, (string, string)[] locParams, out string loc)
	{
		loc = null;
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		string text = overrideLangCode ?? Languages.CurrentLanguage;
		string text2 = Languages.ShortLangCodes[text];
		bool flag = Languages.CurrentLanguage == text;
		if (!flag || !_textCache.TryGetCached(key, out loc))
		{
			lock (_dbConnection)
			{
				if (!_selectCommandsByLangCode.TryGetValue(text2, out var value))
				{
					SqliteCommand sqliteCommand = new SqliteCommand($"SELECT {text2} FROM Loc WHERE Key = @key", _dbConnection)
					{
						Parameters = 
						{
							new SqliteParameter("@key", SqliteType.Text, 250)
							{
								IsNullable = false
							}
						}
					};
					sqliteCommand.CheckedPrepare(_biLogger);
					value = (_selectCommandsByLangCode[text2] = sqliteCommand);
				}
				value.Parameters[0].Value = key;
				using SqliteDataReader sqliteDataReader = value.CheckedExecuteReader(CommandBehavior.SingleResult, _biLogger);
				loc = (sqliteDataReader.Read() ? _stringCache.Get(sqliteDataReader.GetString(0)) : null);
				if (flag)
				{
					_textCache.SetCached(key, loc);
				}
			}
		}
		if (loc != null && locParams != null && locParams.Length != 0)
		{
			for (int i = 0; i < locParams.Length; i++)
			{
				(string, string) tuple = locParams[i];
				string item = tuple.Item1;
				string item2 = tuple.Item2;
				loc = loc.Replace("{" + item + "}", item2);
			}
		}
		return loc != null;
	}

	public string GetLocalizedTextForLanguage(string key, string overrideLangCode, params (string, string)[] locParams)
	{
		TryGetLocalizedTextForLanguage(key, overrideLangCode, locParams, out var loc);
		return loc;
	}

	public bool DoesContainTranslation(string key)
	{
		return GetKeys().Contains(key);
	}

	public bool IsDisposed()
	{
		return false;
	}

	public IEnumerable<string> GetKeys()
	{
		lock (_dbConnection)
		{
			if (_allKeysCache == null)
			{
				HashSet<string> hashSet = new HashSet<string>();
				using SqliteCommand command = new SqliteCommand("SELECT Key FROM Loc", _dbConnection);
				using SqliteDataReader sqliteDataReader = command.CheckedExecuteReader(CommandBehavior.Default, _biLogger);
				while (sqliteDataReader.Read())
				{
					string item = sqliteDataReader.GetString(0);
					hashSet.Add(item);
				}
				_allKeysCache = hashSet;
			}
		}
		return _allKeysCache;
	}

	public void CloseConnection()
	{
		if (_dbConnection != null)
		{
			_logger.Info("Closing connection to " + _connectionString);
		}
		else
		{
			_logger.Info("Could not close connection to " + _connectionString + " as it was null");
		}
		_dbConnection?.Close();
		_dbConnection?.Dispose();
		_dbConnection = null;
		_textCache?.ClearCache();
		_stringCache?.ClearCache();
		_allKeysCache = null;
	}

	public void Dispose()
	{
		_logger.Info("Disposing SqlLocalizationManager");
		Languages.LanguageChangedSignal.Listeners -= OnLanguageChanged;
		CloseConnection();
		foreach (KeyValuePair<string, SqliteCommand> item in _selectCommandsByLangCode)
		{
			item.Value?.Dispose();
		}
		_selectCommandsByLangCode.Clear();
	}
}
