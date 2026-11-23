using UnityEngine;

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

        public void Sync(bool globalEnable, float dt)
        {
            if (vrTarget == null || ikTarget == null) return;

            Vector3 targetPos = vrTarget.TransformPoint(positionOffset);

            // 補正が有効な場合のみ信号処理パイプラインを通す
            if (globalEnable && useEnhancer)
            {
                targetPos = enhancer.Process(targetPos, dt);
            }
            else
            {
                enhancer.Reset();
            }

            ikTarget.position = targetPos;
            ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(rotationOffset);
        }
    }

    [Header("全体の補正ON/OFF（A/Bテスト用）")]
    public bool enableCorrection = true;

    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        // 1. 体全体のルート（足元）をヘッドセットの水平位置に追従
        if (head.vrTarget != null)
        {
            Vector3 bodyPosition = new Vector3(head.vrTarget.position.x, transform.position.y, head.vrTarget.position.z);
            transform.position = bodyPosition;

            float yaw = head.vrTarget.eulerAngles.y;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, yaw, 0), dt * 5f);
        }

        // 2. 各部位のIKを同期（手のみ補正を適用するのがおすすめ）
        head.Sync(enableCorrection, dt);
        leftHand.Sync(enableCorrection, dt);
        rightHand.Sync(enableCorrection, dt);
    }
}