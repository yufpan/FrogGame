using UnityEngine;
using UnityEngine.UI;

public class BGCanvas : MonoBehaviour
{
    [SerializeField] private Image _bgImage;
    [SerializeField] private Sprite _menuBGSprite;
    [SerializeField] private Sprite _gameBGSprite;

    private void Start()
    {
        _bgImage.sprite = _menuBGSprite;
    }

    public void SetGameBGSprite()
    {
        _bgImage.sprite = _gameBGSprite;
    }

    public void SetMenuBGSprite()
    {
        _bgImage.sprite = _menuBGSprite;
    }
}