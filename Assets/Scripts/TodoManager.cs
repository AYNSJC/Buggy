using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[Serializable]
public class TaskData {
    public string taskName;
    public int urgency; // Dropdown index
    public bool isCompleted; // New checkbox state
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

    // Drag and drop variables
    private bool isDragging = false;
    private GameObject draggedObject = null;
    private Vector3 dragStartPosition;
    private Transform originalParent;
    private int originalSiblingIndex;

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

        TaskData newTask = new TaskData { taskName = "New Task", urgency = 0, isCompleted = false };
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

            // Add drag and drop functionality to group
            AddGroupDragAndDrop(groupObj, index);
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
            Toggle completedToggle = taskObj.transform.Find("CompletedToggle").GetComponent<Toggle>();
            Button deleteButton = taskObj.transform.Find("DeleteButton").GetComponent<Button>();

            taskNameInput.text = tasks[taskIndex].taskName;
            urgencyDropdown.value = tasks[taskIndex].urgency;
            completedToggle.isOn = tasks[taskIndex].isCompleted;

            taskNameInput.onValueChanged.AddListener((val) => UpdateTaskName(taskIndex, val));
            urgencyDropdown.onValueChanged.AddListener((val) => UpdateTaskUrgency(taskIndex, val));
            completedToggle.onValueChanged.AddListener((val) => UpdateTaskCompleted(taskIndex, val));
            deleteButton.onClick.AddListener(() => DeleteTask(taskIndex));

            // Add visual feedback for completed tasks
            UpdateTaskVisuals(taskObj, tasks[taskIndex].isCompleted);

            // Add drag and drop functionality to task
            AddTaskDragAndDrop(taskObj, taskIndex);
        }
    }

    private void UpdateTaskVisuals(GameObject taskObj, bool isCompleted) {
        TMP_InputField taskNameInput = taskObj.transform.Find("TaskNameInput").GetComponent<TMP_InputField>();

        if(isCompleted) {
            taskNameInput.textComponent.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Darker gray
            taskNameInput.textComponent.fontStyle = FontStyles.Strikethrough | FontStyles.Bold; // Bold strikethrough
            taskNameInput.textComponent.alpha = 0.7f; // Slightly transparent
        }
        else {
            taskNameInput.textComponent.color = Color.black;
            taskNameInput.textComponent.fontStyle = FontStyles.Normal;
            taskNameInput.textComponent.alpha = 1f; // Fully opaque
        }
    }

    private void AddGroupDragAndDrop(GameObject groupObj, int groupIndex) {
        GroupDragHandler dragHandler = groupObj.AddComponent<GroupDragHandler>();
        dragHandler.Initialize(this, groupIndex);
    }

    private void AddTaskDragAndDrop(GameObject taskObj, int taskIndex) {
        TaskDragHandler dragHandler = taskObj.AddComponent<TaskDragHandler>();
        dragHandler.Initialize(this, taskIndex);
    }

    public void OnGroupDragStart(GameObject draggedGroup, int groupIndex) {
        isDragging = true;
        draggedObject = draggedGroup;
        dragStartPosition = draggedGroup.transform.position;
        originalParent = draggedGroup.transform.parent;
        originalSiblingIndex = draggedGroup.transform.GetSiblingIndex();

        // Make the dragged object semi-transparent
        CanvasGroup canvasGroup = draggedGroup.GetComponent<CanvasGroup>();
        if(canvasGroup == null) canvasGroup = draggedGroup.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.6f;

        // Bring to front for better visual feedback
        draggedGroup.transform.SetAsLastSibling();
    }

    public void OnGroupDrag(GameObject draggedGroup, Vector3 position) {
        // Update visual position
        draggedGroup.transform.position = position;

        // Find the closest drop position based on cursor location
        int closestIndex = GetClosestGroupIndex(position);

        // Update sibling index for visual feedback
        if(closestIndex >= 0 && closestIndex != draggedGroup.transform.GetSiblingIndex()) {
            draggedGroup.transform.SetSiblingIndex(closestIndex);
        }
    }

    public void OnGroupDragEnd(GameObject draggedGroup, int originalGroupIndex) {
        isDragging = false;

        // Restore opacity
        CanvasGroup canvasGroup = draggedGroup.GetComponent<CanvasGroup>();
        if(canvasGroup != null) canvasGroup.alpha = 1f;

        // Find where to drop based on final cursor position
        int targetIndex = GetClosestGroupIndex(draggedGroup.transform.position);

        if(targetIndex < 0) {
            // No valid drop position, return to original
            targetIndex = originalGroupIndex;
        }

        // Set the final sibling index
        draggedGroup.transform.SetSiblingIndex(targetIndex);

        // Snap to proper grid position
        LayoutRebuilder.ForceRebuildLayoutImmediate(groupListContent as RectTransform);

        // Only proceed with data reordering if there was actually a change
        if(targetIndex != originalGroupIndex) {
            // Reorder the groups data
            GroupData movedGroup = groups[originalGroupIndex];
            groups.RemoveAt(originalGroupIndex);
            groups.Insert(targetIndex, movedGroup);

            // Update current group index if needed
            if(currentGroupIndex == originalGroupIndex) {
                currentGroupIndex = targetIndex;
            }
            else if(currentGroupIndex > originalGroupIndex && currentGroupIndex <= targetIndex) {
                currentGroupIndex--;
            }
            else if(currentGroupIndex < originalGroupIndex && currentGroupIndex >= targetIndex) {
                currentGroupIndex++;
            }

            SaveData();
            RefreshGroupUI();
        }

        draggedObject = null;
    }

    public void OnTaskDragStart(GameObject draggedTask, int taskIndex) {
        isDragging = true;
        draggedObject = draggedTask;
        dragStartPosition = draggedTask.transform.position;
        originalParent = draggedTask.transform.parent;
        originalSiblingIndex = draggedTask.transform.GetSiblingIndex();

        // Make the dragged object semi-transparent
        CanvasGroup canvasGroup = draggedTask.GetComponent<CanvasGroup>();
        if(canvasGroup == null) canvasGroup = draggedTask.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.6f;

        // Bring to front for better visual feedback
        draggedTask.transform.SetAsLastSibling();
    }

    public void OnTaskDrag(GameObject draggedTask, Vector3 position) {
        // Update visual position
        draggedTask.transform.position = position;

        if(currentGroupIndex < 0) return;

        // Find the closest drop position based on cursor location
        int closestIndex = GetClosestTaskIndex(position);

        // Update sibling index for visual feedback
        if(closestIndex >= 0 && closestIndex != draggedTask.transform.GetSiblingIndex()) {
            draggedTask.transform.SetSiblingIndex(closestIndex);
        }
    }

    public void OnTaskDragEnd(GameObject draggedTask, int originalTaskIndex) {
        isDragging = false;

        // Restore opacity
        CanvasGroup canvasGroup = draggedTask.GetComponent<CanvasGroup>();
        if(canvasGroup != null) canvasGroup.alpha = 1f;

        if(currentGroupIndex < 0) {
            draggedObject = null;
            return;
        }

        // Find where to drop based on final cursor position
        int targetIndex = GetClosestTaskIndex(draggedTask.transform.position);

        if(targetIndex < 0) {
            // No valid drop position, return to original
            targetIndex = originalTaskIndex;
        }

        // Set the final sibling index
        draggedTask.transform.SetSiblingIndex(targetIndex);

        // Snap to proper grid position
        LayoutRebuilder.ForceRebuildLayoutImmediate(taskListContent as RectTransform);

        // Only proceed with data reordering if there was actually a change
        if(targetIndex != originalTaskIndex) {
            // Reorder the tasks data
            TaskData movedTask = groups[currentGroupIndex].tasks[originalTaskIndex];
            groups[currentGroupIndex].tasks.RemoveAt(originalTaskIndex);
            groups[currentGroupIndex].tasks.Insert(targetIndex, movedTask);

            SaveData();
            RefreshTaskUI();
        }

        draggedObject = null;
    }

    // NEW METHOD: Find closest group index based on cursor position
    private int GetClosestGroupIndex(Vector3 dragPosition) {
        if(groupListContent.childCount == 0) return -1;

        float closestDistance = float.MaxValue;
        int closestIndex = -1;

        for(int i = 0; i < groupListContent.childCount; i++) {
            Transform child = groupListContent.GetChild(i);
            if(child.gameObject == draggedObject) continue; // Skip the dragged object itself

            float distance = Vector3.Distance(dragPosition, child.position);
            if(distance < closestDistance) {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        // If we're dragging past the last item, place at the end
        if(closestIndex >= 0) {
            Transform closestChild = groupListContent.GetChild(closestIndex);
            if(dragPosition.y < closestChild.position.y) {
                // Below the closest item, so place after it
                closestIndex = Mathf.Min(closestIndex + 1, groups.Count - 1);
            }
        }

        return closestIndex;
    }

    // NEW METHOD: Find closest task index based on cursor position
    private int GetClosestTaskIndex(Vector3 dragPosition) {
        if(taskListContent.childCount == 0 || currentGroupIndex < 0) return -1;

        float closestDistance = float.MaxValue;
        int closestIndex = -1;

        for(int i = 0; i < taskListContent.childCount; i++) {
            Transform child = taskListContent.GetChild(i);
            if(child.gameObject == draggedObject) continue; // Skip the dragged object itself

            float distance = Vector3.Distance(dragPosition, child.position);
            if(distance < closestDistance) {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        // If we're dragging past the last item, place at the end
        if(closestIndex >= 0) {
            Transform closestChild = taskListContent.GetChild(closestIndex);
            if(dragPosition.y < closestChild.position.y) {
                // Below the closest item, so place after it
                closestIndex = Mathf.Min(closestIndex + 1, groups[currentGroupIndex].tasks.Count - 1);
            }
        }

        return closestIndex;
    }

    // REMOVED: GetAdjacentGroupIndex and GetAdjacentTaskIndex methods (no longer needed)

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

    private void UpdateTaskCompleted(int taskIndex, bool isCompleted) {
        groups[currentGroupIndex].tasks[taskIndex].isCompleted = isCompleted;
        SaveData();

        // Update visual feedback
        GameObject taskObj = taskListContent.GetChild(taskIndex).gameObject;
        UpdateTaskVisuals(taskObj, isCompleted);
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

// Drag handler for groups
public class GroupDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
    private TodoManager todoManager;
    public int groupIndex; // Made public so TodoManager can access it
    private Canvas canvas;

    public void Initialize(TodoManager manager, int index) {
        todoManager = manager;
        groupIndex = index;
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData) {
        todoManager.OnGroupDragStart(gameObject, groupIndex);
    }

    public void OnDrag(PointerEventData eventData) {
        Vector3 worldPosition;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            transform.parent as RectTransform,
            eventData.position,
            canvas.worldCamera,
            out worldPosition);

        todoManager.OnGroupDrag(gameObject, worldPosition);
    }

    public void OnEndDrag(PointerEventData eventData) {
        todoManager.OnGroupDragEnd(gameObject, groupIndex);
    }
}

// Drag handler for tasks
public class TaskDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
    private TodoManager todoManager;
    public int taskIndex; // Made public so TodoManager can access it
    private Canvas canvas;

    public void Initialize(TodoManager manager, int index) {
        todoManager = manager;
        taskIndex = index;
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData) {
        todoManager.OnTaskDragStart(gameObject, taskIndex);
    }

    public void OnDrag(PointerEventData eventData) {
        Vector3 worldPosition;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            transform.parent as RectTransform,
            eventData.position,
            canvas.worldCamera,
            out worldPosition);

        todoManager.OnTaskDrag(gameObject, worldPosition);
    }

    public void OnEndDrag(PointerEventData eventData) {
        todoManager.OnTaskDragEnd(gameObject, taskIndex);
    }
}