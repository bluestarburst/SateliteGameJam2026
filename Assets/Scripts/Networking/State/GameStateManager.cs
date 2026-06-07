



using UnityEngine;

public class GameStateManager : MonoBehaviour
{

    public static GameStateManager Instance { get; private set; }

    public bool IsInGame { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void SetInGame(bool inGame)
    {
        IsInGame = inGame;
    }



    public void CreateSateliteEvent() {
        if (IsInGame) {
            // SatelliteEventManager.Instance.CreateSatelliteEvent();
        }
    }


}