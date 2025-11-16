using System;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour {
	public static bool _darkTheme = true;

	[SerializeField] private Image _bg;
	[SerializeField] private Image _panel;
	[SerializeField] private Image _addButton;
	[SerializeField] private Image _addButton2;
	[SerializeField] private Image _closeButton;
	[SerializeField] private Image _themeButton;

	[Header("")]
	[SerializeField] private Sprite _bgSprite;
	[SerializeField] private Sprite _panelSprite;
	[SerializeField] private Sprite _addButtonSprite;
	[SerializeField] private Sprite _closeButtonSprite;
	[SerializeField] private Sprite _themeSprite;

	[Header("")]
	[SerializeField] private Sprite _bgSpriteLight;
	[SerializeField] private Sprite _panelSpriteLight;
	[SerializeField] private Sprite _addButtonSpriteLight;
	[SerializeField] private Sprite _closeButtonSpriteLight;
	[SerializeField] private Sprite _themeSpriteLight;

	public void ChangeTheme() {
		if(_darkTheme) {
			_bg.sprite = _bgSpriteLight;
			_panel.sprite = _panelSpriteLight;
			_addButton.sprite = _addButtonSpriteLight;
			_addButton2.sprite = _addButtonSpriteLight;
			_closeButton.sprite = _closeButtonSpriteLight;
			_themeButton.sprite = _themeSpriteLight;

			_darkTheme = false;
		}
		else {
			_bg.sprite = _bgSprite;
			_panel.sprite = _panelSprite;
			_addButton.sprite = _addButtonSprite;
			_addButton2.sprite = _addButtonSprite;
			_closeButton.sprite = _closeButtonSprite;
			_themeButton.sprite = _themeSprite;

			_darkTheme = true;
		}
	}
}
