using UnityEngine;
using XLua;
using System.IO;

public class XLuaManager : MonoBehaviour
{
    public static XLuaManager Instance { get; private set; }
    
    public LuaEnv LuaEnv { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeLua();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeLua()
    {
        LuaEnv = new LuaEnv();
        
        // Custom Loader to load scripts from specific directories if needed
        // LuaEnv.AddLoader(CustomLoader);

        // Register API
        RegisterGameAPI();
        
        Debug.Log("XLua Engine Initialized");
    }

    private void RegisterGameAPI()
    {
        // Expose Global 'Game' object
        LuaEnv.Global.Set("Game", new XLuaScriptableAPI());
    }

    public void Update()
    {
        if (LuaEnv != null)
        {
            LuaEnv.Tick();
        }
    }

    void OnDestroy()
    {
        if (LuaEnv != null)
        {
            LuaEnv.Dispose();
            LuaEnv = null;
        }
    }

    // Helper to run script string
    public object[] DoString(string scriptContent)
    {
        return LuaEnv.DoString(scriptContent);
    }
    
    // Create an isolated table for a card
    public LuaTable CreateCardInstance(RuntimeCard card, string scriptCode)
    {
        // Execute the script. We expect the script to return a Table.
        // Example script: 
        // local Card = {}
        // function Card.OnPlay(self) ... end
        // return Card
        
        object[] results = DoString(scriptCode);
        if (results != null && results.Length > 0 && results[0] is LuaTable cardTable)
        {
            // Inject C# object 'Card' into the Lua table
            cardTable.Set("Card", card);
            
            // Call OnInit if exists
            var onInit = cardTable.Get<System.Action<LuaTable>>("OnInit");
            if (onInit != null)
            {
                onInit(cardTable);
            }
            
            return cardTable;
        }
        
        return null;
    }
}
