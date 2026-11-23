using UnityEngine;

[System.Serializable]
public class MotionEnhancer
{
    [Header("1. One Euro Filter (ノイズ除去と遅延抑制)")]
    [Tooltip("最小カットオフ周波数（低いほど静止時に滑らかになる）")]
    public float minCutoff = 1.0f;
    [Tooltip("速度係数（高いほど速い動きの遅延が減る）")]
    public float beta = 0.05f;
    [Tooltip("微分用カットオフ周波数")]
    public float dCutoff = 1.0f;

    [Header("2. キレの強調 (位相進み・速度外挿)")]
    [Tooltip("速度による予測外挿の強さ（0で無効、0.02〜0.06程度が目安）")]
    [Range(0f, 0.15f)]
    public float sharpnessGain = 0.03f;
    [Tooltip("外挿による最大オフセット距離(m)（暴走防止のクランプ）")]
    public float maxExtrapolation = 0.12f;

    [Header("3. 静止スナップ (止めの強調)")]
    [Tooltip("この速度(m/s)以下になったら静止とみなしてブレーキをかける")]
    public float stopSpeedThreshold = 0.15f;

    // 内部状態
    private bool initialized = false;
    private Vector3 prevFilteredPos;
    private Vector3 prevDerivative;

    public void Reset()
    {
        initialized = false;
    }

    /// <summary>
    /// 生の座標を受け取り、キレを強調した補正後の座標を返す
    /// </summary>
    public Vector3 Process(Vector3 rawPos, float dt)
    {
        if (dt <= 0f) return rawPos;

        if (!initialized)
        {
            prevFilteredPos = rawPos;
            prevDerivative = Vector3.zero;
            initialized = true;
            return rawPos;
        }

        // --- ステップ1: One Euro Filter ---
        // 生の速度（時間微分）を計算し、ノイズを抑えるため軽く平滑化
        Vector3 rawDerivative = (rawPos - prevFilteredPos) / dt;
        float alphaD = ComputeAlpha(dt, dCutoff);
        Vector3 filteredDerivative = Vector3.Lerp(prevDerivative, rawDerivative, alphaD);

        // 速度の大きさに応じてカットオフ周波数を動的に変更
        float speed = filteredDerivative.magnitude;
        float cutoff = minCutoff + beta * speed;
        float alpha = ComputeAlpha(dt, cutoff);

        Vector3 filteredPos = Vector3.Lerp(prevFilteredPos, rawPos, alpha);

        // 状態更新
        prevFilteredPos = filteredPos;
        prevDerivative = filteredDerivative;

        // --- ステップ2 & 3: 静止スナップ と キレの強調（位相進み補償） ---
        if (speed < stopSpeedThreshold)
        {
            // 静止域：外挿をゼロにしてピタッと止める
            return filteredPos;
        }
        else
        {
            // 動作域：速度方向にわずかに先行させてキレを出す
            // 閾値付近で急激に飛ばないよう、速度に応じて滑らかにゲインを立ち上げる
            float ramp = Mathf.Clamp01((speed - stopSpeedThreshold) / stopSpeedThreshold);
            Vector3 leadOffset = filteredDerivative * (sharpnessGain * ramp);

            // 異常な飛び出しを防ぐため最大距離でクランプ
            if (leadOffset.magnitude > maxExtrapolation)
            {
                leadOffset = leadOffset.normalized * maxExtrapolation;
            }

            return filteredPos + leadOffset;
        }
    }

    private float ComputeAlpha(float dt, float cutoff)
    {
        float tau = 1.0f / (2.0f * Mathf.PI * cutoff);
        return 1.0f / (1.0f + tau / dt);
    }
}