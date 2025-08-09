using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[Serializable]
public class TaskData {
    public string taskName;
    public int urgency; // Dropdown index
}

[Serializable]
public class GroupData {
    public string name;
    public List<TaskData> tasks = new List<TaskData>();
}

public class TodoManager : MonoBehaviour {
    [Header("Prefabs & Containers")]
    public GameObject groupButtonPrefab;
    public Transform groupListContent;
    public GameObject taskItemPrefab;
    public Transform taskListContent;

    [Header("UI Buttons")]
    public Button addGroupButton;
    public Button addTaskButton;

    [Header("UI Text")]
    public TMP_Text currentGroupTitle; // Displays current group name

    [Header("Save Data")]
    public string saveKey = "TodoSaveData";

    private List<GroupData> groups = new List<GroupData>();
    private int currentGroupIndex = -1;

    [Serializable]
    private class SaveWrapper { public List<GroupData> groups; }

    private void Start() {
        LoadData();
        RefreshGroupUI();

        addGroupButton.onClick.AddListener(AddGroup);
        addTaskButton.onClick.AddListener(AddTask);
    }

    private void AddGroup() {
        GroupData newGroup = new GroupData { name = "New Group" };
        groups.Add(newGroup);
        SaveData();
        RefreshGroupUI();
    }

    private void AddTask() {
        if(currentGroupIndex < 0) return;

        TaskData newTask = new TaskData { taskName = "New Task", urgency = 0 };
        groups[currentGroupIndex].tasks.Add(newTask);
        SaveData();
        RefreshTaskUI();
    }

    private void RefreshGroupUI() {
        foreach(Transform child in groupListContent)
            Destroy(child.gameObject);

        for(int i = 0; i < groups.Count; i++) {
            int index = i;
            GameObject groupObj = Instantiate(groupButtonPrefab, groupListContent);

            TMP_InputField nameInput = groupObj.transform.Find("NameInput").GetComponent<TMP_InputField>();
            Button openButton = groupObj.transform.Find("OpenButton").GetComponent<Button>();
            Button deleteButton = groupObj.transform.Find("DeleteButton").GetComponent<Button>();

            nameInput.text = groups[index].name;
            nameInput.onValueChanged.AddListener((val) => UpdateGroupName(index, val));

            openButton.onClick.AddListener(() => OpenGroup(index));
            deleteButton.onClick.AddListener(() => DeleteGroup(index));
        }
    }

    private void RefreshTaskUI() {
        foreach(Transform child in taskListContent)
            Destroy(child.gameObject);

        if(currentGroupIndex < 0) return;

        var tasks = groups[currentGroupIndex].tasks;

        for(int i = 0; i < tasks.Count; i++) {
            int taskIndex = i;
            GameObject taskObj = Instantiate(taskItemPrefab, taskListContent);

            TMP_InputField taskNameInput = taskObj.transform.Find("TaskNameInput").GetComponent<TMP_InputField>();
            TMP_Dropdown urgencyDropdown = taskObj.transform.Find("UrgencyDropdown").GetComponent<TMP_Dropdown>();
            Button deleteButton = taskObj.transform.Find("DeleteButton").GetComponent<Button>();

            taskNameInput.text = tasks[taskIndex].taskName;
            urgencyDropdown.value = tasks[taskIndex].urgency;

            taskNameInput.onValueChanged.AddListener((val) => UpdateTaskName(taskIndex, val));
            urgencyDropdown.onValueChanged.AddListener((val) => UpdateTaskUrgency(taskIndex, val));
            deleteButton.onClick.AddListener(() => DeleteTask(taskIndex));
        }
    }

    private void OpenGroup(int index) {
        currentGroupIndex = index;

        // Set top title
        if(currentGroupTitle != null)
            currentGroupTitle.text = groups[index].name;

        RefreshTaskUI();
    }

    private void UpdateGroupName(int index, string newName) {
        groups[index].name = newName;
        SaveData();

        if(index == currentGroupIndex && currentGroupTitle != null)
            currentGroupTitle.text = newName;
    }

    private void UpdateTaskName(int taskIndex, string newName) {
        groups[currentGroupIndex].tasks[taskIndex].taskName = newName;
        SaveData();
    }

    private void UpdateTaskUrgency(int taskIndex, int newUrgency) {
        groups[currentGroupIndex].tasks[taskIndex].urgency = newUrgency;
        SaveData();
    }

    private void DeleteGroup(int index) {
        groups.RemoveAt(index);
        if(index == currentGroupIndex) {
            currentGroupIndex = -1;
            if(currentGroupTitle != null) currentGroupTitle.text = "";
        }
        SaveData();
        RefreshGroupUI();
        RefreshTaskUI();
    }

    private void DeleteTask(int taskIndex) {
        groups[currentGroupIndex].tasks.RemoveAt(taskIndex);
        SaveData();
        RefreshTaskUI();
    }

    private void SaveData() {
        SaveWrapper wrapper = new SaveWrapper { groups = groups };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();
    }

    private void LoadData() {
        if(PlayerPrefs.HasKey(saveKey)) {
            string json = PlayerPrefs.GetString(saveKey);
            SaveWrapper wrapper = JsonUtility.FromJson<SaveWrapper>(json);
            if(wrapper != null && wrapper.groups != null)
                groups = wrapper.groups;
        }
    }
}
