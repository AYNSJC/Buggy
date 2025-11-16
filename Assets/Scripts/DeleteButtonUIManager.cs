using UnityEngine;
using UnityEngine.UI;

public class DeleteButtonUIManager : MonoBehaviour {
	[SerializeField] private Sprite _darkUIButtonSprite;
	[SerializeField] private Sprite _lightUIButtonSprite;

	private void LateUpdate() {
		if(UIManager._darkTheme) {
			if(this.GetComponent<Image>().sprite != _darkUIButtonSprite){
				this.GetComponent<Image>().sprite = _darkUIButtonSprite;
			}
		}
		else {
			if(this.GetComponent<Image>().sprite != _lightUIButtonSprite) {
				this.GetComponent<Image>().sprite= _lightUIButtonSprite;
			}
		}
	}
}
