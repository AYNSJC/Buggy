using UnityEngine;

public class Manager : MonoBehaviour {
    [SerializeField] private GameObject _task;
    [SerializeField] private GameObject _content;

    public void AddBug() {
        Instantiate(_task, _content.transform);
    }
}
