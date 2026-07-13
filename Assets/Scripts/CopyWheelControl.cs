using UnityEngine;

public class CopyWheelControl : MonoBehaviour
{
    public Transform originalWheel; // Оригинальное колесо (модель)

    private void Update()
    {
        if (originalWheel != null && transform.parent != null)
        {
            // Получаем локальные координаты оригинального колеса относительно его родителя
            Vector3 originalLocalPosition = originalWheel.parent.InverseTransformPoint(originalWheel.position);
            Quaternion originalLocalRotation = Quaternion.Inverse(originalWheel.parent.rotation) * originalWheel.rotation;

            // Применяем локальные координаты оригинального колеса к колесу копии
            transform.position = transform.parent.TransformPoint(originalLocalPosition);
            transform.rotation = transform.parent.rotation * originalLocalRotation;
        }
    }
}