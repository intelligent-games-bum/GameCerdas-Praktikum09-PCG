using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Tombol yang bereaksi saat mouse DITEKAN, bukan saat dilepas.
/// CameraFollow mengunci kursor lagi begitu klik kiri ditekan, sehingga Button.onClick
/// (yang menunggu mouse dilepas di atas tombol) tidak pernah terpanggil.
/// </summary>
public class HudPressButton : MonoBehaviour, IPointerDownHandler
{
    public UnityEvent onPress = new UnityEvent();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            onPress.Invoke();
    }
}
