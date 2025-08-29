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
    private Vector3 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;

    [Serializable]
    private class SaveWrapper { public List<GroupData> groups; }

    private void Start() {
        LoadData();
        RefreshGroupUI();

        addGroupButton.onClick.AddListener(AddGroup);
        addTaskButton.onClick.AddListener(AddTask);

        // Add cursor change for buttons
        AddCursorChangeToButton(addGroupButton);
        AddCursorChangeToButton(addTaskButton);
    }

    private void AddCursorChangeToButton(Button button) {
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if(trigger == null) {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        // Mouse enter event
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware); });
        trigger.triggers.Add(enterEntry);

        // Mouse exit event
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); });
        trigger.triggers.Add(exitEntry);
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
        originalPosition = draggedGroup.transform.position;
        originalParent = draggedGroup.transform.parent;
        originalSiblingIndex = draggedGroup.transform.GetSiblingIndex();

        // Make the dragged object semi-transparent
        CanvasGroup canvasGroup = draggedGroup.GetComponent<CanvasGroup>();
        if(canvasGroup == null) canvasGroup = draggedGroup.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.6f;
    }

    public void OnGroupDrag(GameObject draggedGroup, Vector3 position) {
        // Get original index from the drag handler
        GroupDragHandler handler = draggedGroup.GetComponent<GroupDragHandler>();
        int originalIndex = handler.groupIndex;
        bool isLastGroup = (originalIndex == groups.Count - 1);

        if(isLastGroup) {
            // For last group, heavily restrict movement - only allow position swapping with second-to-last
            int currentSiblingIndex = draggedGroup.transform.GetSiblingIndex();
            Vector3 currentPosition = groupListContent.GetChild(originalIndex).position;

            float dragDistance = position.y - currentPosition.y;
            float moveThreshold = 80f; // Higher threshold for last item

            if(dragDistance > moveThreshold && groups.Count > 1) {
                // Can only move to second-to-last position
                draggedGroup.transform.SetSiblingIndex(groups.Count - 2);
            }
            else if(dragDistance <= moveThreshold || currentSiblingIndex != groups.Count - 1) {
                // Snap back to last position
                draggedGroup.transform.SetSiblingIndex(groups.Count - 1);
            }
        }
        else {
            // Normal drag behavior for non-last items
            draggedGroup.transform.position = position;

            int currentIndex = draggedGroup.transform.GetSiblingIndex();
            int targetIndex = GetAdjacentGroupIndex(position, currentIndex);

            if(targetIndex != -1 && targetIndex != currentIndex) {
                draggedGroup.transform.SetSiblingIndex(targetIndex);
            }
        }
    }

    public void OnGroupDragEnd(GameObject draggedGroup, int originalGroupIndex) {
        isDragging = false;

        // Restore opacity
        CanvasGroup canvasGroup = draggedGroup.GetComponent<CanvasGroup>();
        if(canvasGroup != null) canvasGroup.alpha = 1f;

        int newIndex = draggedGroup.transform.GetSiblingIndex();

        // Extra protection for last group - force it to valid position
        bool wasLastGroup = (originalGroupIndex == groups.Count - 1);
        if(wasLastGroup) {
            // Last group can only be in last position or second-to-last position
            if(newIndex < groups.Count - 2) {
                newIndex = groups.Count - 1; // Force back to last position
                draggedGroup.transform.SetSiblingIndex(newIndex);
            }
        }

        // Only proceed with data reordering if there was actually a valid change
        if(newIndex != originalSiblingIndex && newIndex != originalGroupIndex) {
            // Reorder the groups data
            GroupData movedGroup = groups[originalGroupIndex];
            groups.RemoveAt(originalGroupIndex);

            // Ensure we don't insert beyond bounds
            newIndex = Mathf.Clamp(newIndex, 0, groups.Count);
            groups.Insert(newIndex, movedGroup);

            // Update current group index if needed
            if(currentGroupIndex == originalGroupIndex) {
                currentGroupIndex = newIndex;
            }
            else if(currentGroupIndex > originalGroupIndex && currentGroupIndex <= newIndex) {
                currentGroupIndex--;
            }
            else if(currentGroupIndex < originalGroupIndex && currentGroupIndex >= newIndex) {
                currentGroupIndex++;
            }

            SaveData();
            RefreshGroupUI();
        }
        else {
            // Force back to original/valid position
            draggedGroup.transform.SetSiblingIndex(originalSiblingIndex);
        }

        draggedObject = null;
    }

    public void OnTaskDragStart(GameObject draggedTask, int taskIndex) {
        isDragging = true;
        draggedObject = draggedTask;
        originalPosition = draggedTask.transform.position;
        originalParent = draggedTask.transform.parent;
        originalSiblingIndex = draggedTask.transform.GetSiblingIndex();

        // Make the dragged object semi-transparent
        CanvasGroup canvasGroup = draggedTask.GetComponent<CanvasGroup>();
        if(canvasGroup == null) canvasGroup = draggedTask.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.6f;
    }

    public void OnTaskDrag(GameObject draggedTask, Vector3 position) {
        // Get original index from the drag handler
        TaskDragHandler handler = draggedTask.GetComponent<TaskDragHandler>();
        int originalIndex = handler.taskIndex;
        bool isLastTask = (currentGroupIndex >= 0 && originalIndex == groups[currentGroupIndex].tasks.Count - 1);

        if(isLastTask && currentGroupIndex >= 0) {
            // For last task, heavily restrict movement - only allow position swapping with second-to-last
            int currentSiblingIndex = draggedTask.transform.GetSiblingIndex();
            Vector3 currentPosition = taskListContent.GetChild(originalIndex).position;

            float dragDistance = position.y - currentPosition.y;
            float moveThreshold = 80f; // Higher threshold for last item

            if(dragDistance > moveThreshold && groups[currentGroupIndex].tasks.Count > 1) {
                // Can only move to second-to-last position
                draggedTask.transform.SetSiblingIndex(groups[currentGroupIndex].tasks.Count - 2);
            }
            else if(dragDistance <= moveThreshold || currentSiblingIndex != groups[currentGroupIndex].tasks.Count - 1) {
                // Snap back to last position
                draggedTask.transform.SetSiblingIndex(groups[currentGroupIndex].tasks.Count - 1);
            }
        }
        else {
            // Normal drag behavior for non-last items
            draggedTask.transform.position = position;

            int currentIndex = draggedTask.transform.GetSiblingIndex();
            int targetIndex = GetAdjacentTaskIndex(position, currentIndex);

            if(targetIndex != -1 && targetIndex != currentIndex) {
                draggedTask.transform.SetSiblingIndex(targetIndex);
            }
        }
    }

    public void OnTaskDragEnd(GameObject draggedTask, int originalTaskIndex) {
        isDragging = false;

        // Restore opacity
        CanvasGroup canvasGroup = draggedTask.GetComponent<CanvasGroup>();
        if(canvasGroup != null) canvasGroup.alpha = 1f;

        int newIndex = draggedTask.transform.GetSiblingIndex();

        // Extra protection for last task - force it to valid position
        bool wasLastTask = (currentGroupIndex >= 0 && originalTaskIndex == groups[currentGroupIndex].tasks.Count - 1);
        if(wasLastTask && currentGroupIndex >= 0) {
            // Last task can only be in last position or second-to-last position
            if(newIndex < groups[currentGroupIndex].tasks.Count - 2) {
                newIndex = groups[currentGroupIndex].tasks.Count - 1; // Force back to last position
                draggedTask.transform.SetSiblingIndex(newIndex);
            }
        }

        // Only proceed with data reordering if there was actually a valid change
        if(newIndex != originalSiblingIndex && currentGroupIndex >= 0 && newIndex != originalTaskIndex) {
            // Reorder the tasks data
            TaskData movedTask = groups[currentGroupIndex].tasks[originalTaskIndex];
            groups[currentGroupIndex].tasks.RemoveAt(originalTaskIndex);

            // Ensure we don't insert beyond bounds
            newIndex = Mathf.Clamp(newIndex, 0, groups[currentGroupIndex].tasks.Count);
            groups[currentGroupIndex].tasks.Insert(newIndex, movedTask);

            SaveData();
            RefreshTaskUI();
        }
        else {
            // Force back to original/valid position
            draggedTask.transform.SetSiblingIndex(originalSiblingIndex);
        }

        draggedObject = null;
    }

    private int GetAdjacentGroupIndex(Vector3 dragPosition, int currentIndex) {
        if(groupListContent.childCount <= 1) return -1;

        // Special handling for last group
        bool isLastGroup = (currentIndex == groups.Count - 1);

        // Get the current item's position
        Vector3 currentPosition = groupListContent.GetChild(currentIndex).position;

        // Calculate drag distance (positive = dragging up, negative = dragging down)
        float dragDistance = dragPosition.y - currentPosition.y;

        // Only move if dragged far enough (threshold to prevent jittery movement)
        float moveThreshold = 50f;

        if(dragDistance > moveThreshold && currentIndex > 0) {
            // Dragging up, move to previous position
            int targetIndex = currentIndex - 1;
            // Extra validation for last group
            if(isLastGroup) {
                targetIndex = Mathf.Clamp(targetIndex, 0, groups.Count - 2);
            }
            return targetIndex;
        }
        else if(dragDistance < -moveThreshold && currentIndex < groupListContent.childCount - 1) {
            // Dragging down, move to next position
            int targetIndex = currentIndex + 1;
            // Extra validation for last group (it can't move further down if it's already last)
            if(isLastGroup) {
                return currentIndex; // Last group can't move down
            }
            return Mathf.Clamp(targetIndex, 0, groups.Count - 1);
        }

        return currentIndex; // No change
    }

    private int GetAdjacentTaskIndex(Vector3 dragPosition, int currentIndex) {
        if(taskListContent.childCount <= 1 || currentGroupIndex < 0) return -1;

        // Special handling for last task
        bool isLastTask = (currentIndex == groups[currentGroupIndex].tasks.Count - 1);

        // Get the current item's position
        Vector3 currentPosition = taskListContent.GetChild(currentIndex).position;

        // Calculate drag distance (positive = dragging up, negative = dragging down)
        float dragDistance = dragPosition.y - currentPosition.y;

        // Only move if dragged far enough (threshold to prevent jittery movement)
        float moveThreshold = 50f;

        if(dragDistance > moveThreshold && currentIndex > 0) {
            // Dragging up, move to previous position
            int targetIndex = currentIndex - 1;
            // Extra validation for last task
            if(isLastTask) {
                targetIndex = Mathf.Clamp(targetIndex, 0, groups[currentGroupIndex].tasks.Count - 2);
            }
            return targetIndex;
        }
        else if(dragDistance < -moveThreshold && currentIndex < taskListContent.childCount - 1) {
            // Dragging down, move to next position
            int targetIndex = currentIndex + 1;
            // Extra validation for last task (it can't move further down if it's already last)
            if(isLastTask) {
                return currentIndex; // Last task can't move down
            }
            return Mathf.Clamp(targetIndex, 0, groups[currentGroupIndex].tasks.Count - 1);
        }

        return currentIndex; // No change
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
        transform.SetAsLastSibling(); // Bring to front
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
        transform.SetAsLastSibling(); // Bring to front
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