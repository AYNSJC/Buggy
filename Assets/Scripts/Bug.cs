using UnityEngine;

public class Bug : MonoBehaviour {
    [SerializeField] private GameObject _this;

    public void DeleteBug() {
        Destroy(_this);
    }
}
