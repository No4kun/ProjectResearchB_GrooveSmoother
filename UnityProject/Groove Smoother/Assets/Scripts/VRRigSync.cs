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

        public void Sync()
        {
            ikTarget.position = vrTarget.TransformPoint(positionOffset);
            ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(rotationOffset);
        }
    }

    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    void LateUpdate()
    {
        // 1. 体全体のルート（足元）を、ヘッドセットのX・Z座標（水平位置）に追従させる
        // 高さは床(アバターの現在のY)を維持
        Vector3 bodyPosition = new Vector3(head.vrTarget.position.x, transform.position.y, head.vrTarget.position.z);
        transform.position = bodyPosition;

        // 体の向き（Y軸回転）をヘッドセットの向きに滑らかに追従させる
        float yaw = head.vrTarget.eulerAngles.y + 180f;
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, yaw, 0), Time.deltaTime * 5f);

        // 2. その後、各部位のIKを同期
        head.Sync();
        leftHand.Sync();
        rightHand.Sync();
    }
}