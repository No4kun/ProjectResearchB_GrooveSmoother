using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VRRigSync : MonoBehaviour
{
    [System.Serializable]
    public class VRMap
    {
        public Transform vrTarget;
        public Transform ikTarget;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;

        [Header("モーション補正設定")]
        public bool useEnhancer = false;
        public MotionEnhancer enhancer = new MotionEnhancer();

        [HideInInspector] public Vector3 lastRawPos;
        [HideInInspector] public Vector3 lastOutPos;
        [HideInInspector] public float lastRawSpeed;
        [HideInInspector] public float lastOutSpeed;

        private bool hasPrev = false;

        public void Sync(bool globalEnable, float dt)
        {
            if (vrTarget == null || ikTarget == null) return;

            Vector3 rawPos = vrTarget.TransformPoint(positionOffset);
            Vector3 outPos = rawPos;

            if (globalEnable && useEnhancer)
            {
                outPos = enhancer.Process(rawPos, dt);
            }
            else
            {
                enhancer.Reset();
            }

            if (hasPrev && dt > 0f)
            {
                lastRawSpeed = Vector3.Distance(rawPos, lastRawPos) / dt;
                lastOutSpeed = Vector3.Distance(outPos, lastOutPos) / dt;
            }
            else
            {
                lastRawSpeed = 0f;
                lastOutSpeed = 0f;
                hasPrev = true;
            }

            lastRawPos = rawPos;
            lastOutPos = outPos;

            ikTarget.position = outPos;
            ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(rotationOffset);
        }
    }

    [Header("全体の補正設定")]
    public bool enableCorrection = true;
    [Range(0f, 0.15f)]
    public float currentSharpnessGain = 0.03f;
    public float gainStep = 0.01f;

    [Header("状態確認・調整用UI（任意）")]
    public Renderer statusIndicator;
    public TMP_Text statusText;
    public Slider gainSlider;

    [Header("トラッキング部位")]
    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    private InputAction toggleCorrectionAction;
    private InputAction toggleLoggingAction;
    private InputAction increaseGainAction;
    private InputAction decreaseGainAction;

    private bool isLogging = false;
    private StringBuilder csvBuffer;
    private float logStartTime;

    void OnEnable()
    {
        // 右手Aボタン / Cキー：補正ON/OFF
        toggleCorrectionAction = new InputAction("ToggleCorrection", InputActionType.Button);
        toggleCorrectionAction.AddBinding("<XRController>{RightHand}/primaryButton");
        toggleCorrectionAction.AddBinding("<Keyboard>/c");
        toggleCorrectionAction.performed += OnToggleCorrection;
        toggleCorrectionAction.Enable();

        // 右手Bボタン / Lキー：CSV記録ON/OFF
        toggleLoggingAction = new InputAction("ToggleLogging", InputActionType.Button);
        toggleLoggingAction.AddBinding("<XRController>{RightHand}/secondaryButton");
        toggleLoggingAction.AddBinding("<Keyboard>/l");
        toggleLoggingAction.performed += OnToggleLogging;
        toggleLoggingAction.Enable();

        // 左手Yボタン / ↑キー：Sharpness Gain 増加
        increaseGainAction = new InputAction("IncreaseGain", InputActionType.Button);
        increaseGainAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
        increaseGainAction.AddBinding("<Keyboard>/upArrow");
        increaseGainAction.performed += OnIncreaseGain;
        increaseGainAction.Enable();

        // 左手Xボタン / ↓キー：Sharpness Gain 減少
        decreaseGainAction = new InputAction("DecreaseGain", InputActionType.Button);
        decreaseGainAction.AddBinding("<XRController>{LeftHand}/primaryButton");
        decreaseGainAction.AddBinding("<Keyboard>/downArrow");
        decreaseGainAction.performed += OnDecreaseGain;
        decreaseGainAction.Enable();

        // シークバーの初期設定とイベント登録
        if (gainSlider != null)
        {
            gainSlider.minValue = 0f;
            gainSlider.maxValue = 0.15f;
            gainSlider.value = currentSharpnessGain;
            gainSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        ApplyGainToHands();
        UpdateStatusUI();
    }

    void OnDisable()
    {
        if (toggleCorrectionAction != null)
        {
            toggleCorrectionAction.performed -= OnToggleCorrection;
            toggleCorrectionAction.Disable();
            toggleCorrectionAction.Dispose();
        }
        if (toggleLoggingAction != null)
        {
            toggleLoggingAction.performed -= OnToggleLogging;
            toggleLoggingAction.Disable();
            toggleLoggingAction.Dispose();
        }
        if (increaseGainAction != null)
        {
            increaseGainAction.performed -= OnIncreaseGain;
            increaseGainAction.Disable();
            increaseGainAction.Dispose();
        }
        if (decreaseGainAction != null)
        {
            decreaseGainAction.performed -= OnDecreaseGain;
            decreaseGainAction.Disable();
            decreaseGainAction.Dispose();
        }

        if (gainSlider != null)
        {
            gainSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        if (isLogging) SaveCsv();
    }

    private void OnIncreaseGain(InputAction.CallbackContext ctx)
    {
        if (!enableCorrection) return;
        AdjustGain(gainStep);
    }

    private void OnDecreaseGain(InputAction.CallbackContext ctx)
    {
        if (!enableCorrection) return;
        AdjustGain(-gainStep);
    }

    private void AdjustGain(float delta)
    {
        currentSharpnessGain = Mathf.Clamp(currentSharpnessGain + delta, 0f, 0.15f);
        currentSharpnessGain = Mathf.Round(currentSharpnessGain * 1000f) / 1000f;
        ApplyGainToHands();
        UpdateStatusUI();
        Debug.Log($"[VRRigSync] Sharpness Gain: {currentSharpnessGain:F3}");
    }

    private void OnSliderValueChanged(float value)
    {
        if (!enableCorrection) return;

        currentSharpnessGain = Mathf.Round(value * 1000f) / 1000f;
        ApplyGainToHands();
        UpdateStatusUI();
    }

    private void ApplyGainToHands()
    {
        if (leftHand?.enhancer != null) leftHand.enhancer.sharpnessGain = currentSharpnessGain;
        if (rightHand?.enhancer != null) rightHand.enhancer.sharpnessGain = currentSharpnessGain;
    }

    private void OnToggleCorrection(InputAction.CallbackContext ctx)
    {
        enableCorrection = !enableCorrection;
        Debug.Log($"[VRRigSync] Correction: {(enableCorrection ? "ON" : "OFF")}");
        UpdateStatusUI();
    }

    private void OnToggleLogging(InputAction.CallbackContext ctx)
    {
        if (!isLogging) StartLogging();
        else SaveCsv();
        UpdateStatusUI();
    }

    private void StartLogging()
    {
        isLogging = true;
        logStartTime = Time.time;
        csvBuffer = new StringBuilder();
        csvBuffer.AppendLine(
            "Time,CorrectionEnabled,SharpnessGain," +
            "R_RawX,R_RawY,R_RawZ,R_OutX,R_OutY,R_OutZ,R_RawSpeed,R_OutSpeed," +
            "L_RawX,L_RawY,L_RawZ,L_OutX,L_OutY,L_OutZ,L_RawSpeed,L_OutSpeed"
        );
        Debug.Log("[VRRigSync] CSV Logging Started.");
    }

    private void SaveCsv()
    {
        isLogging = false;
        if (csvBuffer == null || csvBuffer.Length == 0) return;

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filePath = Path.Combine(Application.persistentDataPath, $"motion_log_{timestamp}.csv");

        try
        {
            File.WriteAllText(filePath, csvBuffer.ToString(), Encoding.UTF8);
            Debug.Log($"[VRRigSync] CSV Saved to: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VRRigSync] Failed to save CSV: {e.Message}");
        }
        csvBuffer.Clear();
    }

    private void UpdateStatusUI()
    {
        if (statusIndicator != null)
        {
            if (isLogging) statusIndicator.material.color = Color.red;
            else if (enableCorrection) statusIndicator.material.color = Color.green;
            else statusIndicator.material.color = Color.white;
        }

        if (gainSlider != null)
        {
            gainSlider.interactable = enableCorrection;
            gainSlider.SetValueWithoutNotify(currentSharpnessGain);
        }

        if (statusText != null)
        {
            string corrState = enableCorrection ? "ON" : "OFF";
            string recState = isLogging ? " [REC]" : "";
            statusText.text = $"Correction: {corrState}{recState}\nSharpness Gain: {currentSharpnessGain:F3}";
        }
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (head.vrTarget != null)
        {
            Vector3 bodyPosition = new Vector3(head.vrTarget.position.x, transform.position.y, head.vrTarget.position.z);
            transform.position = bodyPosition;

            float yaw = head.vrTarget.eulerAngles.y;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, yaw, 0), dt * 5f);
        }

        head.Sync(enableCorrection, dt);
        leftHand.Sync(enableCorrection, dt);
        rightHand.Sync(enableCorrection, dt);

        if (isLogging)
        {
            float t = Time.time - logStartTime;
            int corr = enableCorrection ? 1 : 0;
            csvBuffer.AppendFormat(
                "{0:F4},{1},{2:F3}," +
                "{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4}," +
                "{11:F4},{12:F4},{13:F4},{14:F4},{15:F4},{16:F4},{17:F4},{18:F4}\n",
                t, corr, currentSharpnessGain,
                rightHand.lastRawPos.x, rightHand.lastRawPos.y, rightHand.lastRawPos.z,
                rightHand.lastOutPos.x, rightHand.lastOutPos.y, rightHand.lastOutPos.z,
                rightHand.lastRawSpeed, rightHand.lastOutSpeed,
                leftHand.lastRawPos.x, leftHand.lastRawPos.y, leftHand.lastRawPos.z,
                leftHand.lastOutPos.x, leftHand.lastOutPos.y, leftHand.lastOutPos.z,
                leftHand.lastRawSpeed, leftHand.lastOutSpeed
            );
        }
    }
}