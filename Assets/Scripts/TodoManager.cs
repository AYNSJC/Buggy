using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[Serializable]
public class SubTaskData {
	public string subTaskName;
	public bool isCompleted;
}

[Serializable]
public class TaskData {
	public string taskName;
	public int urgency; // Dropdown index
	public bool isCompleted;
	public bool isExpanded; // For showing/hiding subtasks
	public List<SubTaskData> subTasks = new List<SubTaskData>();
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
	public GameObject subTaskItemPrefab; // NEW: Add this prefab in inspector

	[Header("UI Buttons")]
	public Button addGroupButton;
	public Button addTaskButton;

	[Header("UI Text")]
	public TMP_Text currentGroupTitle;

	[Header("Save Data")]
	public string saveKey = "TodoSaveData";

	private List<GroupData> groups = new List<GroupData>();
	private int currentGroupIndex = -1;

	// Drag and drop variables
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

		TaskData newTask = new TaskData { taskName = "New Task", urgency = 0, isCompleted = false, isExpanded = false };
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

			// Subtask UI elements
			Button expandButton = taskObj.transform.Find("ExpandButton").GetComponent<Button>();
			Button addSubTaskButton = taskObj.transform.Find("AddSubTaskButton").GetComponent<Button>();

			taskNameInput.text = tasks[taskIndex].taskName;
			urgencyDropdown.value = tasks[taskIndex].urgency;
			completedToggle.isOn = tasks[taskIndex].isCompleted;

			taskNameInput.onValueChanged.AddListener((val) => UpdateTaskName(taskIndex, val));
			urgencyDropdown.onValueChanged.AddListener((val) => UpdateTaskUrgency(taskIndex, val));
			completedToggle.onValueChanged.AddListener((val) => UpdateTaskCompleted(taskIndex, val));
			deleteButton.onClick.AddListener(() => DeleteTask(taskIndex));

			// Subtask button listeners
			expandButton.onClick.AddListener(() => ToggleSubTasks(taskIndex));
			addSubTaskButton.onClick.AddListener(() => AddSubTask(taskIndex));

			// Update expand button text/icon
			TMP_Text expandButtonText = expandButton.GetComponentInChildren<TMP_Text>();
			if(expandButtonText != null) {
				expandButtonText.text = tasks[taskIndex].isExpanded ? "▼" : "►";
			}

			// Show/hide add subtask button based on expansion state
			addSubTaskButton.gameObject.SetActive(tasks[taskIndex].isExpanded);

			UpdateTaskVisuals(taskObj, tasks[taskIndex].isCompleted);
			AddTaskDragAndDrop(taskObj, taskIndex);

			// CHANGED: Add subtasks directly to main list instead of container
			if(tasks[taskIndex].isExpanded) {
				var subTasks = tasks[taskIndex].subTasks;
				for(int j = 0; j < subTasks.Count; j++) {
					int subTaskIndex = j;
					GameObject subTaskObj = Instantiate(subTaskItemPrefab, taskListContent);

					TMP_InputField subTaskNameInput = subTaskObj.transform.Find("SubTaskNameInput").GetComponent<TMP_InputField>();
					Toggle subTaskCompletedToggle = subTaskObj.transform.Find("SubTaskCompletedToggle").GetComponent<Toggle>();
					Button deleteSubTaskButton = subTaskObj.transform.Find("DeleteSubTaskButton").GetComponent<Button>();

					subTaskNameInput.text = subTasks[subTaskIndex].subTaskName;
					subTaskCompletedToggle.isOn = subTasks[subTaskIndex].isCompleted;

					subTaskNameInput.onValueChanged.AddListener((val) => UpdateSubTaskName(taskIndex, subTaskIndex, val));
					subTaskCompletedToggle.onValueChanged.AddListener((val) => UpdateSubTaskCompleted(taskIndex, subTaskIndex, val));
					deleteSubTaskButton.onClick.AddListener(() => DeleteSubTask(taskIndex, subTaskIndex));

					// FIXED: Apply visual styling immediately when creating subtask
					UpdateSubTaskVisuals(subTaskObj, subTasks[subTaskIndex].isCompleted);
				}
			}
		}

		// Force layout rebuild after all tasks are created
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(taskListContent as RectTransform);
	}

	private void RefreshSubTasksUI(int taskIndex, Transform subTaskContainer) {
		// This method is no longer needed since subtasks are added directly to main list
		// Keeping it empty for backward compatibility
	}

	private void ToggleSubTasks(int taskIndex) {
		groups[currentGroupIndex].tasks[taskIndex].isExpanded = !groups[currentGroupIndex].tasks[taskIndex].isExpanded;
		SaveData();
		RefreshTaskUI();
	}

	private void AddSubTask(int taskIndex) {
		if(currentGroupIndex < 0) return;

		SubTaskData newSubTask = new SubTaskData { subTaskName = "New Subtask", isCompleted = false };
		groups[currentGroupIndex].tasks[taskIndex].subTasks.Add(newSubTask);
		SaveData();
		RefreshTaskUI();
	}

	private void UpdateSubTaskName(int taskIndex, int subTaskIndex, string newName) {
		groups[currentGroupIndex].tasks[taskIndex].subTasks[subTaskIndex].subTaskName = newName;
		SaveData();
	}

	private void UpdateSubTaskCompleted(int taskIndex, int subTaskIndex, bool isCompleted) {
		groups[currentGroupIndex].tasks[taskIndex].subTasks[subTaskIndex].isCompleted = isCompleted;
		SaveData();

		// Refresh the entire UI to update visual feedback
		RefreshTaskUI();
	}

	private void DeleteSubTask(int taskIndex, int subTaskIndex) {
		groups[currentGroupIndex].tasks[taskIndex].subTasks.RemoveAt(subTaskIndex);
		SaveData();
		RefreshTaskUI();
	}

	private void UpdateTaskVisuals(GameObject taskObj, bool isCompleted) {
		TMP_InputField taskNameInput = taskObj.transform.Find("TaskNameInput").GetComponent<TMP_InputField>();

		if(isCompleted) {
			taskNameInput.textComponent.color = new Color(0.5f, 0.5f, 0.5f, 1f);
			taskNameInput.textComponent.fontStyle = FontStyles.Strikethrough | FontStyles.Bold;
			taskNameInput.textComponent.alpha = 0.7f;
		}
		else {
			taskNameInput.textComponent.color = Color.black;
			taskNameInput.textComponent.fontStyle = FontStyles.Normal;
			taskNameInput.textComponent.alpha = 1f;
		}
	}

	private void UpdateSubTaskVisuals(GameObject subTaskObj, bool isCompleted) {
		TMP_InputField subTaskNameInput = subTaskObj.transform.Find("SubTaskNameInput").GetComponent<TMP_InputField>();

		// Force the text component to update immediately
		if(subTaskNameInput != null && subTaskNameInput.textComponent != null) {
			if(isCompleted) {
				subTaskNameInput.textComponent.color = new Color(0.6f, 0.6f, 0.6f, 1f);
				subTaskNameInput.textComponent.fontStyle = FontStyles.Strikethrough;
				subTaskNameInput.textComponent.alpha = 0.8f;
			}
			else {
				subTaskNameInput.textComponent.color = Color.black;
				subTaskNameInput.textComponent.fontStyle = FontStyles.Normal;
				subTaskNameInput.textComponent.alpha = 1f;
			}

			// Force the text to rebuild immediately
			subTaskNameInput.textComponent.SetAllDirty();
			Canvas.ForceUpdateCanvases();
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
		draggedObject = draggedGroup;
		dragStartPosition = draggedGroup.transform.position;
		originalParent = draggedGroup.transform.parent;
		originalSiblingIndex = draggedGroup.transform.GetSiblingIndex();

		CanvasGroup canvasGroup = draggedGroup.GetComponent<CanvasGroup>();
		if(canvasGroup == null) canvasGroup = draggedGroup.AddComponent<CanvasGroup>();
		canvasGroup.alpha = 0.6f;

		draggedGroup.transform.SetAsLastSibling();
	}

	public void OnGroupDrag(GameObject draggedGroup, Vector3 position) {
		draggedGroup.transform.position = position;

		int closestIndex = GetClosestGroupIndex(position);

		if(closestIndex >= 0) {
			// Ensure we don't exceed the actual child count
			int validIndex = Mathf.Min(closestIndex, groupListContent.childCount - 1);
			if(validIndex != draggedGroup.transform.GetSiblingIndex()) {
				draggedGroup.transform.SetSiblingIndex(validIndex);
			}
		}
	}

	public void OnGroupDragEnd(GameObject draggedGroup, int originalGroupIndex) {
		CanvasGroup canvasGroup = draggedGroup.GetComponent<CanvasGroup>();
		if(canvasGroup != null) canvasGroup.alpha = 1f;

		int targetIndex = GetClosestGroupIndex(draggedGroup.transform.position);

		if(targetIndex < 0) {
			targetIndex = originalGroupIndex;
		}

		draggedGroup.transform.SetSiblingIndex(targetIndex);
		LayoutRebuilder.ForceRebuildLayoutImmediate(groupListContent as RectTransform);

		if(targetIndex != originalGroupIndex) {
			GroupData movedGroup = groups[originalGroupIndex];
			groups.RemoveAt(originalGroupIndex);
			groups.Insert(targetIndex, movedGroup);

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
		draggedObject = draggedTask;
		dragStartPosition = draggedTask.transform.position;
		originalParent = draggedTask.transform.parent;
		originalSiblingIndex = draggedTask.transform.GetSiblingIndex();

		CanvasGroup canvasGroup = draggedTask.GetComponent<CanvasGroup>();
		if(canvasGroup == null) canvasGroup = draggedTask.AddComponent<CanvasGroup>();
		canvasGroup.alpha = 0.6f;

		draggedTask.transform.SetAsLastSibling();
	}

	public void OnTaskDrag(GameObject draggedTask, Vector3 position) {
		draggedTask.transform.position = position;

		if(currentGroupIndex < 0) return;

		int closestIndex = GetClosestTaskIndex(position);

		if(closestIndex >= 0) {
			// Ensure we don't exceed the actual child count
			int validIndex = Mathf.Min(closestIndex, taskListContent.childCount - 1);
			if(validIndex != draggedTask.transform.GetSiblingIndex()) {
				draggedTask.transform.SetSiblingIndex(validIndex);
			}
		}
	}

	public void OnTaskDragEnd(GameObject draggedTask, int originalTaskIndex) {
		CanvasGroup canvasGroup = draggedTask.GetComponent<CanvasGroup>();
		if(canvasGroup != null) canvasGroup.alpha = 1f;

		if(currentGroupIndex < 0) {
			draggedObject = null;
			return;
		}

		int targetIndex = GetClosestTaskIndex(draggedTask.transform.position);

		if(targetIndex < 0) {
			targetIndex = originalTaskIndex;
		}

		draggedTask.transform.SetSiblingIndex(targetIndex);
		LayoutRebuilder.ForceRebuildLayoutImmediate(taskListContent as RectTransform);

		if(targetIndex != originalTaskIndex) {
			TaskData movedTask = groups[currentGroupIndex].tasks[originalTaskIndex];
			groups[currentGroupIndex].tasks.RemoveAt(originalTaskIndex);
			groups[currentGroupIndex].tasks.Insert(targetIndex, movedTask);

			SaveData();
			RefreshTaskUI();
		}

		draggedObject = null;
	}

	// FIXED: Improved drag positioning with proper boundary handling
	private int GetClosestGroupIndex(Vector3 dragPosition) {
		if(groupListContent.childCount == 0) return -1;
		if(groups.Count == 0) return 0;

		int validChildCount = 0;
		for(int i = 0; i < groupListContent.childCount; i++) {
			if(groupListContent.GetChild(i).gameObject != draggedObject) {
				validChildCount++;
			}
		}

		if(validChildCount == 0) return 0;

		// Check if dragging above the first non-dragged item
		for(int i = 0; i < groupListContent.childCount; i++) {
			Transform child = groupListContent.GetChild(i);
			if(child.gameObject != draggedObject) {
				if(dragPosition.y > child.position.y) {
					return 0; // Place at top
				}
				break;
			}
		}

		// Check if dragging below the last non-dragged item
		for(int i = groupListContent.childCount - 1; i >= 0; i--) {
			Transform child = groupListContent.GetChild(i);
			if(child.gameObject != draggedObject) {
				if(dragPosition.y < child.position.y) {
					return i + 1; // Place after last item
				}
				break;
			}
		}

		// Find closest item in the middle
		float closestDistance = float.MaxValue;
		int closestIndex = 0;

		for(int i = 0; i < groupListContent.childCount; i++) {
			Transform child = groupListContent.GetChild(i);
			if(child.gameObject == draggedObject) continue;

			RectTransform rectTransform = child as RectTransform;
			Vector3 itemCenter = rectTransform.position;

			float distance = Mathf.Abs(dragPosition.y - itemCenter.y);

			if(distance < closestDistance) {
				closestDistance = distance;
				closestIndex = i;
			}
		}

		// Determine if we should place before or after the closest item
		Transform closestChild = groupListContent.GetChild(closestIndex);
		if(closestChild.gameObject != draggedObject) {
			if(dragPosition.y < closestChild.position.y) {
				closestIndex = closestIndex + 1;
			}
		}

		return closestIndex;
	}

	// FIXED: Improved drag positioning with proper boundary handling
	private int GetClosestTaskIndex(Vector3 dragPosition) {
		if(taskListContent.childCount == 0 || currentGroupIndex < 0) return -1;
		if(groups[currentGroupIndex].tasks.Count == 0) return 0;

		int validChildCount = 0;
		for(int i = 0; i < taskListContent.childCount; i++) {
			if(taskListContent.GetChild(i).gameObject != draggedObject) {
				validChildCount++;
			}
		}

		if(validChildCount == 0) return 0;

		// Check if dragging above the first non-dragged item
		for(int i = 0; i < taskListContent.childCount; i++) {
			Transform child = taskListContent.GetChild(i);
			if(child.gameObject != draggedObject) {
				if(dragPosition.y > child.position.y) {
					return 0; // Place at top
				}
				break;
			}
		}

		// Check if dragging below the last non-dragged item
		for(int i = taskListContent.childCount - 1; i >= 0; i--) {
			Transform child = taskListContent.GetChild(i);
			if(child.gameObject != draggedObject) {
				if(dragPosition.y < child.position.y) {
					return i + 1; // Place after last item
				}
				break;
			}
		}

		// Find closest item in the middle
		float closestDistance = float.MaxValue;
		int closestIndex = 0;

		for(int i = 0; i < taskListContent.childCount; i++) {
			Transform child = taskListContent.GetChild(i);
			if(child.gameObject == draggedObject) continue;

			RectTransform rectTransform = child as RectTransform;
			Vector3 itemCenter = rectTransform.position;

			float distance = Mathf.Abs(dragPosition.y - itemCenter.y);

			if(distance < closestDistance) {
				closestDistance = distance;
				closestIndex = i;
			}
		}

		// Determine if we should place before or after the closest item
		Transform closestChild = taskListContent.GetChild(closestIndex);
		if(closestChild.gameObject != draggedObject) {
			if(dragPosition.y < closestChild.position.y) {
				closestIndex = closestIndex + 1;
			}
		}

		return closestIndex;
	}

	private void OpenGroup(int index) {
		currentGroupIndex = index;

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

public class GroupDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
	private TodoManager todoManager;
	public int groupIndex;
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

public class TaskDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
	private TodoManager todoManager;
	public int taskIndex;
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