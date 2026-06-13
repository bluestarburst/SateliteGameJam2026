using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SatelliteGameJam.Gameplay.Puzzles;
using SatelliteGameJam.Networking;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.State;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ConsoleInteraction : MonoBehaviour, IInteractable
{
    private const byte ConsoleIdle = 0;
    private const byte ConsoleOccupied = 1;
    private const byte PayloadVersion = 1;

    [Header("Network")]
    [SerializeField] private uint consoleId = 1;

    [Header("Puzzle")]
    [SerializeField] private SatellitePuzzleComponent puzzle;
    [SerializeField] private List<SoftwareFixTarget> softwareFixTargets = new();

    [Header("Terminal UI")]
    [SerializeField] private GameObject terminalRoot;
    [SerializeField] private bool showTerminalRootOnlyWhileLocalInteracting;
    [SerializeField] private TMP_Text terminalText;
    [SerializeField] private TMP_InputField commandInput;
    [SerializeField] private string prompt = "ground@control:~$ ";
    [SerializeField, Min(256)] private int maxOutputCharacters = 4096;
    [SerializeField, Min(0.02f)] private float typingStreamInterval = 0.08f;

    [Header("Camera")]
    public Vector3 cameraPositionOffset;
    public Vector3 lookAtPositionOffset;
    [SerializeField, Min(0f)] private float cameraMoveDuration = 0.5f;

    [Header("Input")]
    [SerializeField] private bool unlockCursorDuringInteraction = true;
    [SerializeField] private bool escapeKeyFallback = true;
    [SerializeField, Min(0.1f)] private float lockRequestTimeout = 2f;

    public Vector3 savedCameraPosition;
    public bool playerAtConsole = false;

    private GroundPlayerInteractor activeInteractor;
    private SteamId currentOwner;
    private string outputBuffer;
    private string currentInput = string.Empty;
    private bool pendingInteraction;
    private bool suppressInputEvents;
    private float pendingInteractionStartedAt;
    private float nextTypingStreamTime;

    private bool HasNetworkSession => SteamManager.Instance != null
        && SteamManager.Instance.PlayerSteamId.Value != 0
        && SteamManager.Instance.HasActiveLobby
        && SatelliteStateManager.Instance != null;
    private SteamId LocalOwnerId => HasNetworkSession ? SteamManager.Instance.PlayerSteamId : 0;
    private bool IsLocalOwner => !HasNetworkSession || currentOwner == SteamManager.Instance.PlayerSteamId;
    private bool IsOccupied => HasNetworkSession ? currentOwner.Value != 0 : playerAtConsole || pendingInteraction;

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponent<SatellitePuzzleComponent>();
        }

        if (terminalRoot != null && showTerminalRootOnlyWhileLocalInteracting)
        {
            terminalRoot.SetActive(false);
        }

        if (string.IsNullOrEmpty(outputBuffer))
        {
            outputBuffer = "Satellite Operations Terminal\nType 'help' for available commands.\n";
        }
    }

    private void OnEnable()
    {
        if (SatelliteStateManager.Instance != null)
        {
            SatelliteStateManager.Instance.OnConsoleStateChanged += OnConsoleStateChanged;
        }

        if (commandInput != null)
        {
            commandInput.onValueChanged.AddListener(OnInputChanged);
            commandInput.onSubmit.AddListener(OnInputSubmitted);
            commandInput.interactable = false;
        }
    }

    private void OnDisable()
    {
        if (SatelliteStateManager.Instance != null)
        {
            SatelliteStateManager.Instance.OnConsoleStateChanged -= OnConsoleStateChanged;
        }

        if (commandInput != null)
        {
            commandInput.onValueChanged.RemoveListener(OnInputChanged);
            commandInput.onSubmit.RemoveListener(OnInputSubmitted);
        }

        if (playerAtConsole)
        {
            if (IsLocalOwner)
            {
                RequestConsoleState(ConsoleIdle, 0, outputBuffer, currentInput);
            }
            EndLocalSession(false);
        }
    }

    private void Start()
    {
        ApplyExistingConsoleState();
        RefreshTerminalView();
    }

    private void Update()
    {
        if (pendingInteraction && Time.time - pendingInteractionStartedAt > lockRequestTimeout)
        {
            pendingInteraction = false;
            AppendLocalNotice("Console is busy or not responding.");
        }

        if (!playerAtConsole)
        {
            return;
        }

        if (unlockCursorDuringInteraction)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (escapeKeyFallback && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Escape(activeInteractor);
        }
    }

    public void Interact(GroundPlayerInteractor interactor)
    {
        if (playerAtConsole)
        {
            FocusInput();
            return;
        }

        if (IsOccupied && !IsLocalOwner)
        {
            AppendLocalNotice("Console is already in use.");
            return;
        }

        activeInteractor = interactor;
        pendingInteraction = true;
        pendingInteractionStartedAt = Time.time;
        RequestConsoleState(ConsoleOccupied, LocalOwnerId, outputBuffer, currentInput);

        if (!HasNetworkSession)
        {
            BeginLocalSession();
        }
    }

    public void Escape(GroundPlayerInteractor interactor)
    {
        if (!playerAtConsole && !pendingInteraction)
        {
            return;
        }

        pendingInteraction = false;
        RequestConsoleState(ConsoleIdle, 0, outputBuffer, currentInput);
        EndLocalSession(true);
    }

    public void OnScroll(GroundPlayerInteractor interactor, float vertical)
    {
    }

    private void BeginLocalSession()
    {
        if (playerAtConsole)
        {
            FocusInput();
            return;
        }

        playerAtConsole = true;
        pendingInteraction = false;
        activeInteractor?.lockPlayer();

        if (terminalRoot != null && showTerminalRootOnlyWhileLocalInteracting)
        {
            terminalRoot.SetActive(true);
        }

        if (unlockCursorDuringInteraction)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        StartCoroutine(MoveCameraNextFrame());
        FocusInput();
    }

    private void EndLocalSession(bool moveCameraBack)
    {
        if (!playerAtConsole && !pendingInteraction)
        {
            return;
        }

        playerAtConsole = false;
        pendingInteraction = false;

        if (commandInput != null)
        {
            commandInput.DeactivateInputField();
            commandInput.interactable = false;
        }

        if (terminalRoot != null && showTerminalRootOnlyWhileLocalInteracting)
        {
            terminalRoot.SetActive(false);
        }

        if (moveCameraBack && activeInteractor != null)
        {
            StartCoroutine(MoveCameraBackNextFrame(activeInteractor));
        }
        else
        {
            activeInteractor?.unlockPlayer();
        }

        activeInteractor = null;
    }

    private IEnumerator MoveCameraNextFrame()
    {
        yield return null;

        if (Camera.main == null)
        {
            yield break;
        }

        savedCameraPosition = Camera.main.transform.position;
        Vector3 targetCameraPosition = transform.position + cameraPositionOffset;
        Vector3 targetLookAtPosition = transform.position + lookAtPositionOffset;

        float elapsedTime = 0f;
        Vector3 startingCameraPosition = Camera.main.transform.position;
        Vector3 startingLookAtPosition = Camera.main.transform.position + Camera.main.transform.forward;

        while (elapsedTime < cameraMoveDuration)
        {
            float t = cameraMoveDuration <= 0f ? 1f : elapsedTime / cameraMoveDuration;
            Camera.main.transform.position = Vector3.Lerp(startingCameraPosition, targetCameraPosition, t);
            Vector3 currentLookAtPosition = Vector3.Lerp(startingLookAtPosition, targetLookAtPosition, t);
            Camera.main.transform.LookAt(currentLookAtPosition);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Camera.main.transform.position = targetCameraPosition;
        Camera.main.transform.LookAt(targetLookAtPosition);
    }

    private IEnumerator MoveCameraBackNextFrame(GroundPlayerInteractor interactor)
    {
        yield return null;

        if (Camera.main == null)
        {
            interactor.unlockPlayer();
            yield break;
        }

        Vector3 targetCameraPosition = savedCameraPosition;
        Vector3 targetLookAtPosition = transform.position + lookAtPositionOffset;

        float elapsedTime = 0f;
        Vector3 startingCameraPosition = Camera.main.transform.position;
        Vector3 startingLookAtPosition = targetLookAtPosition;

        while (elapsedTime < cameraMoveDuration)
        {
            float t = cameraMoveDuration <= 0f ? 1f : elapsedTime / cameraMoveDuration;
            Camera.main.transform.position = Vector3.Lerp(startingCameraPosition, targetCameraPosition, t);
            Vector3 currentLookAtPosition = Vector3.Lerp(startingLookAtPosition, targetLookAtPosition, t);
            Camera.main.transform.LookAt(currentLookAtPosition);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Camera.main.transform.position = targetCameraPosition;
        interactor.unlockPlayer();
    }

    private void OnInputChanged(string value)
    {
        if (suppressInputEvents || !playerAtConsole || !IsLocalOwner)
        {
            return;
        }

        currentInput = value ?? string.Empty;
        RefreshTerminalView();

        if (Time.time >= nextTypingStreamTime)
        {
            StreamCurrentState();
            nextTypingStreamTime = Time.time + typingStreamInterval;
        }
    }

    private void OnInputSubmitted(string submittedText)
    {
        if (!playerAtConsole || !IsLocalOwner)
        {
            return;
        }

        string command = (submittedText ?? string.Empty).Trim();
        AppendOutput($"{prompt}{command}");
        ExecuteCommand(command);

        currentInput = string.Empty;
        suppressInputEvents = true;
        if (commandInput != null)
        {
            commandInput.text = string.Empty;
        }
        suppressInputEvents = false;

        StreamCurrentState();
        FocusInput();
    }

    private void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string verb = parts[0].ToLowerInvariant();

        switch (verb)
        {
            case "help":
                AppendOutput("Commands: help, status, clear, restart <target>, repair <target>");
                AppendOutput("Targets: local, solar, antennas, thrusters, shielding, core, or configured software target names.");
                break;
            case "status":
                PrintStatus();
                break;
            case "clear":
                outputBuffer = string.Empty;
                break;
            case "restart":
            case "repair":
                if (parts.Length < 2)
                {
                    AppendOutput($"Usage: {verb} <target>");
                    break;
                }
                RepairTarget(parts[1]);
                break;
            default:
                AppendOutput($"Unknown command '{verb}'. Type 'help'.");
                break;
        }
    }

    private void PrintStatus()
    {
        float health = GameFlowManager.Instance != null ? GameFlowManager.Instance.GetSatelliteHealth() : 100f;
        float condition = GameFlowManager.Instance != null ? GameFlowManager.Instance.GetSatelliteOverallCondition() : 100f;
        AppendOutput($"Satellite health: {health:0}%");
        AppendOutput($"Overall condition: {condition:0}%");
        AppendOutput(IsOccupied ? $"Console owner: {currentOwner}" : "Console owner: none");
    }

    private void RepairTarget(string target)
    {
        string key = NormalizeTarget(target);

        if (key == "local" || key == "self")
        {
            if (puzzle != null)
            {
                puzzle.MarkRepaired();
                AppendOutput("Local terminal subsystem restarted.");
            }
            else
            {
                AppendOutput("No local puzzle component is assigned.");
            }
            return;
        }

        foreach (var configuredTarget in softwareFixTargets)
        {
            if (configuredTarget.Matches(key))
            {
                configuredTarget.ApplyRepair();
                AppendOutput(configuredTarget.SuccessMessage);
                return;
            }
        }

        if (TryParseModule(key, out var moduleId))
        {
            GameFlowManager.Instance?.ReportSatelliteModuleRepair(moduleId);
            AppendOutput($"{moduleId} software restarted.");
            return;
        }

        if (key.StartsWith("component") && int.TryParse(key.Substring("component".Length), out int componentIndex))
        {
            GameFlowManager.Instance?.ReportSatelliteRepair(componentIndex);
            AppendOutput($"Component {componentIndex} restarted.");
            return;
        }

        AppendOutput($"Unknown restart target '{target}'.");
    }

    private void StreamCurrentState()
    {
        if (!IsLocalOwner)
        {
            return;
        }

        RequestConsoleState(ConsoleOccupied, currentOwner, outputBuffer, currentInput);
    }

    private void RequestConsoleState(byte stateByte, SteamId owner, string output, string input)
    {
        byte[] payload = SerializeTerminalState(owner, output, input);

        if (SatelliteStateManager.Instance != null)
        {
            SatelliteStateManager.Instance.RequestConsoleState(consoleId, stateByte, payload);
            return;
        }

        ApplyLocalConsoleState(stateByte, payload);
    }

    private void OnConsoleStateChanged(uint changedConsoleId, ConsoleStateData state)
    {
        if (changedConsoleId != consoleId || state == null)
        {
            return;
        }

        if (TryDeserializeTerminalState(state.Payload, out SteamId owner, out string output, out string input))
        {
            currentOwner = state.StateByte == ConsoleOccupied ? owner : 0;
            outputBuffer = output;
            currentInput = input;
        }
        else
        {
            currentOwner = 0;
            currentInput = string.Empty;
        }

        RefreshTerminalView();

        if (pendingInteraction && IsLocalOwner)
        {
            BeginLocalSession();
        }
        else if (playerAtConsole && !IsLocalOwner)
        {
            EndLocalSession(true);
        }
    }

    private void ApplyExistingConsoleState()
    {
        ConsoleStateData existing = SatelliteStateManager.Instance?.GetConsoleState(consoleId);
        if (existing != null)
        {
            OnConsoleStateChanged(consoleId, existing);
        }
    }

    private void ApplyLocalConsoleState(byte stateByte, byte[] payload)
    {
        var state = new ConsoleStateData
        {
            ConsoleId = consoleId,
            StateByte = stateByte,
            Payload = payload
        };

        OnConsoleStateChanged(consoleId, state);
    }

    private void AppendOutput(string line)
    {
        outputBuffer = string.IsNullOrEmpty(outputBuffer)
            ? line + "\n"
            : outputBuffer + line + "\n";

        if (outputBuffer.Length > maxOutputCharacters)
        {
            outputBuffer = outputBuffer.Substring(outputBuffer.Length - maxOutputCharacters);
        }

        RefreshTerminalView();
    }

    private void AppendLocalNotice(string line)
    {
        AppendOutput(line);
    }

    private void RefreshTerminalView()
    {
        if (terminalText != null)
        {
            terminalText.text = $"{outputBuffer}{prompt}{currentInput}";
        }
    }

    private void FocusInput()
    {
        if (commandInput == null)
        {
            return;
        }

        suppressInputEvents = true;
        commandInput.text = currentInput;
        suppressInputEvents = false;
        commandInput.interactable = playerAtConsole && IsLocalOwner;
        commandInput.ActivateInputField();
        commandInput.Select();
    }

    private byte[] SerializeTerminalState(SteamId owner, string output, string input)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(PayloadVersion);
        writer.Write(owner.Value);
        writer.Write(output ?? string.Empty);
        writer.Write(input ?? string.Empty);
        return stream.ToArray();
    }

    private bool TryDeserializeTerminalState(byte[] payload, out SteamId owner, out string output, out string input)
    {
        owner = 0;
        output = outputBuffer ?? string.Empty;
        input = string.Empty;

        if (payload == null || payload.Length < 9 || payload[0] != PayloadVersion)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(payload);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            byte version = reader.ReadByte();
            if (version != PayloadVersion)
            {
                return false;
            }

            owner = reader.ReadUInt64();
            output = reader.ReadString();
            input = reader.ReadString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeTarget(string target)
    {
        return (target ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "").Replace("-", "");
    }

    private static bool TryParseModule(string target, out SatelliteModuleId moduleId)
    {
        switch (NormalizeTarget(target))
        {
            case "solar":
            case "solarpanels":
                moduleId = SatelliteModuleId.SolarPanels;
                return true;
            case "antenna":
            case "antennas":
                moduleId = SatelliteModuleId.Antennas;
                return true;
            case "thruster":
            case "thrusters":
                moduleId = SatelliteModuleId.Thrusters;
                return true;
            case "shield":
            case "shielding":
                moduleId = SatelliteModuleId.Shielding;
                return true;
            case "core":
            case "coresystems":
                moduleId = SatelliteModuleId.CoreSystems;
                return true;
            default:
                moduleId = SatelliteModuleId.CoreSystems;
                return false;
        }
    }

    [Serializable]
    private class SoftwareFixTarget
    {
        [SerializeField] private string commandName = "target";
        [SerializeField] private SatellitePuzzleComponent puzzle;
        [SerializeField] private bool repairModule;
        [SerializeField] private SatelliteModuleId moduleId = SatelliteModuleId.CoreSystems;
        [SerializeField, Range(0, 31)] private int componentIndex;
        [SerializeField] private bool useComponentIndex;
        [SerializeField] private string successMessage = "Subsystem restarted.";

        public string SuccessMessage => string.IsNullOrWhiteSpace(successMessage)
            ? "Subsystem restarted."
            : successMessage;

        public bool Matches(string target)
        {
            return NormalizeTarget(commandName) == NormalizeTarget(target);
        }

        public void ApplyRepair()
        {
            if (puzzle != null)
            {
                puzzle.MarkRepaired();
                return;
            }

            if (repairModule)
            {
                GameFlowManager.Instance?.ReportSatelliteModuleRepair(moduleId);
                return;
            }

            if (useComponentIndex)
            {
                GameFlowManager.Instance?.ReportSatelliteRepair(componentIndex);
            }
        }
    }
}
