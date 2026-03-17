using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;

// ============================================
// DATA STRUCTURES
// ============================================

[Serializable]
public class SubTaskData {
	public string subTaskName;
	public bool isCompleted;
}

[Serializable]
public class TaskData {
	public string taskName;
	public int urgency;
	public bool isCompleted;
	public bool isExpanded;
	public List<SubTaskData> subTasks = new List<SubTaskData>();
}

[Serializable]
public class ConnectionData {
	public string fromNodeId;
	public string toNodeId;
	public string connectionId;
	public List<Vector3> points = new List<Vector3>();
}

[Serializable]
public class NodeData {
	public string nodeId;
	public string nodeType;
	public Vector2 position;
	public Vector2 size;
	public List<TaskData> tasks = new List<TaskData>();
	public string textContent;
	public string imagePath;
	public byte[] imageData;
	public string groupName;
	public List<string> childNodeIds = new List<string>();
	public Color nodeColor = new Color(0.2f, 0.3f, 0.4f, 1f);
}

[Serializable]
public class CanvasData {
	public List<NodeData> nodes = new List<NodeData>();
	public List<ConnectionData> connections = new List<ConnectionData>();
	public Vector2 canvasOffset;
	public float canvasZoom = 1f;
}

// ============================================
// MAIN AUTO CANVAS MANAGER
// ============================================

public class AutoVisualCanvasSetup : MonoBehaviour {
	private Canvas canvas;
	private RectTransform canvasContent;
	private CanvasData canvasData = new CanvasData();
	private Dictionary<string, VisualNodeRuntime> nodeObjects = new Dictionary<string, VisualNodeRuntime>();
	private Dictionary<string, GameObject> connectionObjects = new Dictionary<string, GameObject>();

	private bool isConnectionMode = false;
	private VisualNodeRuntime connectionStartNode = null;
	private GameObject currentDragLine = null;

	private Vector2 lastMousePosition;
	private bool isPanning = false;
	private string saveKey = "AutoVisualCanvasSaveData";

	private GameObject toolbar;
	private TMP_Text connectionModeText;

	void Start() {
		SetupCanvas();
		LoadCanvas();
		RefreshCanvas();
	}

	void SetupCanvas() {
		// Get or create Canvas
		canvas = GetComponent<Canvas>();
		if(canvas == null) {
			canvas = gameObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			gameObject.AddComponent<CanvasScaler>();
			gameObject.AddComponent<GraphicRaycaster>();
		}

		// Create EventSystem if doesn't exist
		if(FindObjectOfType<EventSystem>() == null) {
			GameObject eventSystem = new GameObject("EventSystem");
			eventSystem.AddComponent<EventSystem>();
			eventSystem.AddComponent<StandaloneInputModule>();
		}

		// Create main canvas content
		GameObject contentObj = new GameObject("CanvasContent");
		contentObj.transform.SetParent(transform, false);
		canvasContent = contentObj.AddComponent<RectTransform>();
		canvasContent.anchorMin = Vector2.zero;
		canvasContent.anchorMax = Vector2.one;
		canvasContent.sizeDelta = new Vector2(5000, 5000);
		canvasContent.anchoredPosition = Vector2.zero;

		// Add grid background
		Image bgImage = contentObj.AddComponent<Image>();
		bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

		CreateToolbar();
	}

	void CreateToolbar() {
		toolbar = new GameObject("Toolbar");
		toolbar.transform.SetParent(transform, false);
		RectTransform toolbarRect = toolbar.AddComponent<RectTransform>();
		toolbarRect.anchorMin = new Vector2(0, 1);
		toolbarRect.anchorMax = new Vector2(1, 1);
		toolbarRect.pivot = new Vector2(0.5f, 1);
		toolbarRect.sizeDelta = new Vector2(0, 60);
		toolbarRect.anchoredPosition = Vector2.zero;

		Image toolbarBg = toolbar.AddComponent<Image>();
		toolbarBg.color = new Color(0.1f, 0.1f, 0.1f, 1f);

		HorizontalLayoutGroup layout = toolbar.AddComponent<HorizontalLayoutGroup>();
		layout.padding = new RectOffset(10, 10, 10, 10);
		layout.spacing = 10;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = true;
		layout.childControlWidth = true;
		layout.childControlHeight = true;

		CreateButton(toolbar.transform, "Add Todo", () => CreateNode("todo"));
		CreateButton(toolbar.transform, "Add Text", () => CreateNode("text"));
		CreateButton(toolbar.transform, "Add Image", () => CreateNode("image"));
		CreateButton(toolbar.transform, "Add Group", () => CreateNode("group"));

		GameObject connBtn = CreateButton(toolbar.transform, "Connection: OFF", ToggleConnectionMode);
		connectionModeText = connBtn.GetComponentInChildren<TMP_Text>();

		CreateButton(toolbar.transform, "Save", SaveCanvas);
		CreateButton(toolbar.transform, "Clear All", ClearAll);
	}

	GameObject CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction action) {
		GameObject btnObj = new GameObject("Button_" + text);
		btnObj.transform.SetParent(parent, false);

		RectTransform rect = btnObj.AddComponent<RectTransform>();
		rect.sizeDelta = new Vector2(150, 40);

		Image img = btnObj.AddComponent<Image>();
		img.color = new Color(0.3f, 0.5f, 0.7f, 1f);

		Button btn = btnObj.AddComponent<Button>();
		btn.onClick.AddListener(action);

		ColorBlock colors = btn.colors;
		colors.normalColor = new Color(0.3f, 0.5f, 0.7f, 1f);
		colors.highlightedColor = new Color(0.4f, 0.6f, 0.8f, 1f);
		colors.pressedColor = new Color(0.2f, 0.4f, 0.6f, 1f);
		btn.colors = colors;

		GameObject textObj = new GameObject("Text");
		textObj.transform.SetParent(btnObj.transform, false);

		RectTransform textRect = textObj.AddComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.sizeDelta = Vector2.zero;

		TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
		tmp.text = text;
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.fontSize = 14;
		tmp.color = Color.white;

		return btnObj;
	}

	void Update() {
		HandlePanning();
		HandleZoom();

		if(isConnectionMode && currentDragLine != null) {
			UpdateDragLine();
		}

		// Update all connections
		foreach(var conn in canvasData.connections) {
			UpdateConnection(conn);
		}
	}

	void HandlePanning() {
		if(Input.GetMouseButtonDown(2) || (Input.GetKey(KeyCode.Space) && Input.GetMouseButtonDown(0))) {
			isPanning = true;
			lastMousePosition = Input.mousePosition;
		}

		if(isPanning && (Input.GetMouseButton(2) || (Input.GetKey(KeyCode.Space) && Input.GetMouseButton(0)))) {
			Vector2 delta = (Vector2)Input.mousePosition - lastMousePosition;
			canvasContent.anchoredPosition += delta / canvasContent.localScale.x;
			canvasData.canvasOffset = canvasContent.anchoredPosition;
			lastMousePosition = Input.mousePosition;
		}

		if(Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(0)) {
			isPanning = false;
		}
	}

	void HandleZoom() {
		float scroll = Input.GetAxis("Mouse ScrollWheel");
		if(scroll != 0) {
			float newZoom = Mathf.Clamp(canvasContent.localScale.x + scroll * 0.1f, 0.3f, 3f);
			canvasContent.localScale = Vector3.one * newZoom;
			canvasData.canvasZoom = newZoom;
		}
	}

	void CreateNode(string nodeType) {
		Vector2 spawnPos = new Vector2(UnityEngine.Random.Range(-500, 500), UnityEngine.Random.Range(-500, 500));

		NodeData newNode = new NodeData {
			nodeId = System.Guid.NewGuid().ToString(),
			nodeType = nodeType,
			position = spawnPos,
			size = new Vector2(300, 400)
		};

		if(nodeType == "todo") {
			newNode.tasks = new List<TaskData>();
			newNode.nodeColor = new Color(0.3f, 0.5f, 0.3f, 1f);
		}
		else if(nodeType == "text") {
			newNode.textContent = "Enter text here...";
			newNode.nodeColor = new Color(0.5f, 0.4f, 0.3f, 1f);
		}
		else if(nodeType == "image") {
			newNode.nodeColor = new Color(0.4f, 0.3f, 0.5f, 1f);
		}
		else if(nodeType == "group") {
			newNode.groupName = "New Group";
			newNode.nodeColor = new Color(0.3f, 0.4f, 0.5f, 1f);
		}

		canvasData.nodes.Add(newNode);
		SaveCanvas();
		RefreshCanvas();
	}

	void RefreshCanvas() {
		foreach(var node in nodeObjects.Values) {
			if(node != null && node.gameObject != null) {
				Destroy(node.gameObject);
			}
		}
		nodeObjects.Clear();

		foreach(var node in canvasData.nodes) {
			CreateNodeVisual(node);
		}

		RefreshConnections();
	}

	void CreateNodeVisual(NodeData data) {
		GameObject nodeObj = new GameObject("Node_" + data.nodeType);
		nodeObj.transform.SetParent(canvasContent, false);

		VisualNodeRuntime nodeScript = nodeObj.AddComponent<VisualNodeRuntime>();
		nodeScript.Initialize(this, data);

		nodeObjects[data.nodeId] = nodeScript;
	}

	void RefreshConnections() {
		foreach(var conn in connectionObjects.Values) {
			if(conn != null) Destroy(conn);
		}
		connectionObjects.Clear();

		foreach(var conn in canvasData.connections) {
			CreateConnectionVisual(conn);
		}
	}

	void CreateConnectionVisual(ConnectionData conn) {
		GameObject lineObj = new GameObject("Connection");
		lineObj.transform.SetParent(canvasContent, false);
		lineObj.transform.SetAsFirstSibling();

		LineRenderer line = lineObj.AddComponent<LineRenderer>();
		line.material = new Material(Shader.Find("Sprites/Default"));
		line.startColor = new Color(0.5f, 0.7f, 1f, 1f);
		line.endColor = new Color(0.5f, 0.7f, 1f, 1f);
		line.startWidth = 5f;
		line.endWidth = 5f;
		line.positionCount = 0;
		line.useWorldSpace = false;

		connectionObjects[conn.connectionId] = lineObj;
	}

	void UpdateConnection(ConnectionData conn) {
		if(!connectionObjects.ContainsKey(conn.connectionId)) return;
		if(!nodeObjects.ContainsKey(conn.fromNodeId) || !nodeObjects.ContainsKey(conn.toNodeId)) return;

		LineRenderer line = connectionObjects[conn.connectionId].GetComponent<LineRenderer>();
		RectTransform fromRect = nodeObjects[conn.fromNodeId].GetComponent<RectTransform>();
		RectTransform toRect = nodeObjects[conn.toNodeId].GetComponent<RectTransform>();

		Vector3 start = fromRect.anchoredPosition;
		Vector3 end = toRect.anchoredPosition;

		Vector3 dir = (end - start);
		float dist = dir.magnitude;
		Vector3 midPoint = start + dir * 0.5f + Vector3.right * Mathf.Sin(dist * 0.01f) * 50f;

		line.positionCount = 20;
		for(int i = 0; i < 20; i++) {
			float t = i / 19f;
			Vector3 p1 = Vector3.Lerp(start, midPoint, t);
			Vector3 p2 = Vector3.Lerp(midPoint, end, t);
			line.SetPosition(i, Vector3.Lerp(p1, p2, t));
		}
	}

	public void StartConnection(VisualNodeRuntime node) {
		if(!isConnectionMode) return;

		connectionStartNode = node;

		GameObject lineObj = new GameObject("TempLine");
		lineObj.transform.SetParent(canvasContent, false);

		LineRenderer line = lineObj.AddComponent<LineRenderer>();
		line.material = new Material(Shader.Find("Sprites/Default"));
		line.startColor = new Color(1f, 1f, 0f, 0.5f);
		line.endColor = new Color(1f, 1f, 0f, 0.5f);
		line.startWidth = 5f;
		line.endWidth = 5f;
		line.positionCount = 2;
		line.useWorldSpace = false;

		currentDragLine = lineObj;
	}

	void UpdateDragLine() {
		if(currentDragLine == null || connectionStartNode == null) return;

		LineRenderer line = currentDragLine.GetComponent<LineRenderer>();
		RectTransform startRect = connectionStartNode.GetComponent<RectTransform>();

		Vector2 mousePos;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			canvasContent, Input.mousePosition, null, out mousePos);

		line.SetPosition(0, startRect.anchoredPosition);
		line.SetPosition(1, mousePos);
	}

	public void EndConnection(VisualNodeRuntime toNode) {
		if(!isConnectionMode || connectionStartNode == null || toNode == connectionStartNode) {
			CancelConnection();
			return;
		}

		ConnectionData newConn = new ConnectionData {
			connectionId = System.Guid.NewGuid().ToString(),
			fromNodeId = connectionStartNode.nodeData.nodeId,
			toNodeId = toNode.nodeData.nodeId
		};

		canvasData.connections.Add(newConn);
		SaveCanvas();
		CancelConnection();
		RefreshConnections();
	}

	void CancelConnection() {
		if(currentDragLine != null) Destroy(currentDragLine);
		currentDragLine = null;
		connectionStartNode = null;
	}

	void ToggleConnectionMode() {
		isConnectionMode = !isConnectionMode;
		connectionModeText.text = isConnectionMode ? "Connection: ON" : "Connection: OFF";
		CancelConnection();
	}

	public void UpdateNodeData(NodeData data) {
		for(int i = 0; i < canvasData.nodes.Count; i++) {
			if(canvasData.nodes[i].nodeId == data.nodeId) {
				canvasData.nodes[i] = data;
				SaveCanvas();
				break;
			}
		}
	}

	public void DeleteNode(string nodeId) {
		canvasData.nodes.RemoveAll(n => n.nodeId == nodeId);
		canvasData.connections.RemoveAll(c => c.fromNodeId == nodeId || c.toNodeId == nodeId);
		SaveCanvas();
		RefreshCanvas();
	}

	void SaveCanvas() {
		string json = JsonUtility.ToJson(canvasData);
		PlayerPrefs.SetString(saveKey, json);
		PlayerPrefs.Save();
		Debug.Log("Canvas Saved!");
	}

	void LoadCanvas() {
		if(PlayerPrefs.HasKey(saveKey)) {
			string json = PlayerPrefs.GetString(saveKey);
			canvasData = JsonUtility.FromJson<CanvasData>(json);
			if(canvasData == null) canvasData = new CanvasData();

			canvasContent.anchoredPosition = canvasData.canvasOffset;
			canvasContent.localScale = Vector3.one * canvasData.canvasZoom;
		}
	}

	void ClearAll() {
		if(UnityEngine.Windows.Speech.ConfirmationResult.Confirmed ==
		   UnityEngine.Windows.Speech.ConfirmationResult.Confirmed) {
			canvasData = new CanvasData();
			SaveCanvas();
			RefreshCanvas();
		}
	}
}

// ============================================
// VISUAL NODE RUNTIME
// ============================================

public class VisualNodeRuntime : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler {
	public NodeData nodeData;
	private AutoVisualCanvasSetup manager;
	private RectTransform rectTransform;
	private bool isDragging = false;
	private Vector2 dragOffset;

	private GameObject contentArea;
	private TMP_InputField mainInput;
	private ScrollRect scrollView;

	public void Initialize(AutoVisualCanvasSetup mgr, NodeData data) {
		manager = mgr;
		nodeData = data;
		rectTransform = gameObject.AddComponent<RectTransform>();
		rectTransform.sizeDelta = data.size;
		rectTransform.anchoredPosition = data.position;

		BuildNodeUI();
	}

	void BuildNodeUI() {
		// Background
		Image bg = gameObject.AddComponent<Image>();
		bg.color = nodeData.nodeColor;

		Shadow shadow = gameObject.AddComponent<Shadow>();
		shadow.effectDistance = new Vector2(3, -3);

		// Header
		GameObject header = new GameObject("Header");
		header.transform.SetParent(transform, false);
		RectTransform headerRect = header.AddComponent<RectTransform>();
		headerRect.anchorMin = new Vector2(0, 1);
		headerRect.anchorMax = new Vector2(1, 1);
		headerRect.pivot = new Vector2(0.5f, 1);
		headerRect.sizeDelta = new Vector2(-10, 40);
		headerRect.anchoredPosition = new Vector2(0, -5);

		Image headerBg = header.AddComponent<Image>();
		headerBg.color = new Color(0, 0, 0, 0.3f);

		// Title
		GameObject titleObj = new GameObject("Title");
		titleObj.transform.SetParent(header.transform, false);
		RectTransform titleRect = titleObj.AddComponent<RectTransform>();
		titleRect.anchorMin = Vector2.zero;
		titleRect.anchorMax = Vector2.one;
		titleRect.offsetMin = new Vector2(10, 0);
		titleRect.offsetMax = new Vector2(-60, 0);

		TMP_Text title = titleObj.AddComponent<TextMeshProUGUI>();
		title.text = GetNodeTitle();
		title.fontSize = 16;
		title.fontStyle = FontStyles.Bold;
		title.color = Color.white;
		title.alignment = TextAlignmentOptions.MidlineLeft;

		// Delete Button
		CreateDeleteButton(header.transform);

		// Content Area
		CreateContentArea();
	}

	string GetNodeTitle() {
		switch(nodeData.nodeType) {
			case "todo": return "📝 Todo List";
			case "text": return "📄 Text Note";
			case "image": return "🖼️ Image";
			case "group": return "📁 " + nodeData.groupName;
			default: return "Node";
		}
	}

	void CreateDeleteButton(Transform parent) {
		GameObject btnObj = new GameObject("DeleteBtn");
		btnObj.transform.SetParent(parent, false);
		RectTransform rect = btnObj.AddComponent<RectTransform>();
		rect.anchorMin = new Vector2(1, 0.5f);
		rect.anchorMax = new Vector2(1, 0.5f);
		rect.pivot = new Vector2(1, 0.5f);
		rect.sizeDelta = new Vector2(35, 35);
		rect.anchoredPosition = new Vector2(-5, 0);

		Image img = btnObj.AddComponent<Image>();
		img.color = new Color(0.8f, 0.2f, 0.2f, 1f);

		Button btn = btnObj.AddComponent<Button>();
		btn.onClick.AddListener(() => manager.DeleteNode(nodeData.nodeId));

		GameObject txtObj = new GameObject("X");
		txtObj.transform.SetParent(btnObj.transform, false);
		RectTransform txtRect = txtObj.AddComponent<RectTransform>();
		txtRect.anchorMin = Vector2.zero;
		txtRect.anchorMax = Vector2.one;
		txtRect.sizeDelta = Vector2.zero;

		TMP_Text txt = txtObj.AddComponent<TextMeshProUGUI>();
		txt.text = "✕";
		txt.fontSize = 20;
		txt.alignment = TextAlignmentOptions.Center;
		txt.color = Color.white;
	}

	void CreateContentArea() {
		contentArea = new GameObject("Content");
		contentArea.transform.SetParent(transform, false);
		RectTransform contentRect = contentArea.AddComponent<RectTransform>();
		contentRect.anchorMin = new Vector2(0, 0);
		contentRect.anchorMax = new Vector2(1, 1);
		contentRect.offsetMin = new Vector2(10, 10);
		contentRect.offsetMax = new Vector2(-10, -55);

		if(nodeData.nodeType == "text") {
			CreateTextEditor();
		}
		else if(nodeData.nodeType == "todo") {
			CreateTodoList();
		}
		else if(nodeData.nodeType == "image") {
			CreateImageViewer();
		}
		else if(nodeData.nodeType == "group") {
			CreateGroupInfo();
		}
	}

	void CreateTextEditor() {
		GameObject inputObj = new GameObject("TextInput");
		inputObj.transform.SetParent(contentArea.transform, false);
		RectTransform inputRect = inputObj.AddComponent<RectTransform>();
		inputRect.anchorMin = Vector2.zero;
		inputRect.anchorMax = Vector2.one;
		inputRect.sizeDelta = Vector2.zero;

		Image inputBg = inputObj.AddComponent<Image>();
		inputBg.color = new Color(1, 1, 1, 0.9f);

		TMP_InputField input = inputObj.AddComponent<TMP_InputField>();
		input.textComponent = CreateTextComponent(inputObj.transform);
		input.text = nodeData.textContent;
		input.lineType = TMP_InputField.LineType.MultiLineNewline;
		input.onValueChanged.AddListener((val) => {
			nodeData.textContent = val;
			manager.UpdateNodeData(nodeData);
		});
	}

	void CreateTodoList() {
		GameObject scrollObj = new GameObject("ScrollView");
		scrollObj.transform.SetParent(contentArea.transform, false);
		RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
		scrollRect.anchorMin = Vector2.zero;
		scrollRect.anchorMax = Vector2.one;
		scrollRect.sizeDelta = Vector2.zero;

		Image scrollBg = scrollObj.AddComponent<Image>();
		scrollBg.color = new Color(1, 1, 1, 0.1f);

		ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
		scroll.horizontal = false;
		scroll.vertical = true;

		GameObject viewport = new GameObject("Viewport");
		viewport.transform.SetParent(scrollObj.transform, false);
		RectTransform viewportRect = viewport.AddComponent<RectTransform>();
		viewportRect.anchorMin = Vector2.zero;
		viewportRect.anchorMax = Vector2.one;
		viewportRect.sizeDelta = Vector2.zero;
		viewport.AddComponent<Image>().color = Color.clear;
		viewport.AddComponent<Mask>().showMaskGraphic = false;

		GameObject content = new GameObject("Content");
		content.transform.SetParent(viewport.transform, false);
		RectTransform contentRect = content.AddComponent<RectTransform>();
		contentRect.anchorMin = new Vector2(0, 1);
		contentRect.anchorMax = new Vector2(1, 1);
		contentRect.pivot = new Vector2(0.5f, 1);
		contentRect.sizeDelta = new Vector2(0, 300);

		VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
		layout.spacing = 5;
		layout.padding = new RectOffset(5, 5, 5, 5);
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		layout.childControlHeight = true;

		ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		scroll.viewport = viewportRect;
		scroll.content = contentRect;

		// Add task button
		CreateAddTaskButton(content.transform);

		// Create existing tasks
		foreach(var task in nodeData.tasks) {
			CreateTaskItem(content.transform, task);
		}
	}

	void CreateAddTaskButton(Transform parent) {
		GameObject btnObj = new GameObject("AddTaskBtn");
		btnObj.transform.SetParent(parent, false);

		LayoutElement layout = btnObj.AddComponent<LayoutElement>();
		layout.preferredHeight = 30;

		Image img = btnObj.AddComponent<Image>();
		img.color = new Color(0.3f, 0.6f, 0.3f, 1f);

		Button btn = btnObj.AddComponent<Button>();
		btn.onClick.AddListener(() => {
			TaskData newTask = new TaskData { taskName = "New Task", urgency = 0, isCompleted = false };
			nodeData.tasks.Add(newTask);
			manager.UpdateNodeData(nodeData);
			BuildNodeUI(); // Rebuild to show new task
		});

		GameObject txtObj = new GameObject("Text");
		txtObj.transform.SetParent(btnObj.transform, false);
		RectTransform txtRect = txtObj.AddComponent<RectTransform>();
		txtRect.anchorMin = Vector2.zero;
		txtRect.anchorMax = Vector2.one;
		txtRect.sizeDelta = Vector2.zero;

		TMP_Text txt = txtObj.AddComponent<TextMeshProUGUI>();
		txt.text = "+ Add Task";
		txt.fontSize = 12;
		txt.alignment = TextAlignmentOptions.Center;
		txt.color = Color.white;
	}

	void CreateTaskItem(Transform parent, TaskData task) {
		GameObject taskObj = new GameObject("Task");
		taskObj.transform.SetParent(parent, false);

		LayoutElement layoutElem = taskObj.AddComponent<LayoutElement>();
		layoutElem.preferredHeight = 35;

		Image taskBg = taskObj.AddComponent<Image>();
		taskBg.color = new Color(1, 1, 1, 0.2f);

		HorizontalLayoutGroup hLayout = taskObj.AddComponent<HorizontalLayoutGroup>();
		hLayout.spacing = 5;
		hLayout.padding = new RectOffset(5, 5, 5, 5);
		hLayout.childForceExpandWidth = true;
		hLayout.childForceExpandHeight = true;
		hLayout.childControlWidth = true;
		hLayout.childControlHeight = true;

		// Checkbox
		GameObject toggleObj = new GameObject("Toggle");
		toggleObj.transform.SetParent(taskObj.transform, false);
		LayoutElement toggleLayout = toggleObj.AddComponent<LayoutElement>();
		toggleLayout.preferredWidth = 25;

		Toggle toggle = toggleObj.AddComponent<Toggle>();
		toggle.isOn = task.isCompleted;
		toggle.onValueChanged.AddListener((val) => {
			task.isCompleted = val;
			manager.UpdateNodeData(nodeData);
		});

		// Task name input
		GameObject inputObj = new GameObject("Input");
		inputObj.transform.SetParent(taskObj.transform, false);
		Image inputBg = inputObj.AddComponent<Image>();
		inputBg.color = new Color(1, 1, 1, 0.8f);

		TMP_InputField input = inputObj.AddComponent<TMP_InputField>();
		input.textComponent = CreateTextComponent(inputObj.transform, 12);
		input.text = task.taskName;
		input.onValueChanged.AddListener((val) => {
			task.taskName = val;
			manager.UpdateNodeData(nodeData);
		});
	}

	void CreateImageViewer() {
		GameObject imgObj = new GameObject("Image");
		imgObj.transform.SetParent(contentArea.transform, false);
		RectTransform imgRect = imgObj.AddComponent<RectTransform>();
		imgRect.anchorMin = Vector2.zero;
		imgRect.anchorMax = Vector2.one;
		imgRect.sizeDelta = Vector2.zero;

		Image img = imgObj.AddComponent<Image>();
		img.color = new Color(0.5f, 0.5f, 0.5f, 1f);

		// Add upload button
		GameObject btnObj = new GameObject("UploadBtn");
		btnObj.transform.SetParent(contentArea.transform, false);
		RectTransform btnRect = btnObj.AddComponent<RectTransform>();
		btnRect.anchorMin = new Vector2(0.5f, 0.5f);
		btnRect.anchorMax = new Vector2(0.5f, 0.5f);
		btnRect.sizeDelta = new Vector2(120, 40);

		Image btnImg = btnObj.AddComponent<Image>();
		btnImg.color = new Color(0.3f, 0.5f, 0.7f, 1f);

		Button btn = btnObj.AddComponent<Button>();
		btn.onClick.AddListener(() => Debug.Log("Image upload - implement file picker"));

		GameObject txtObj = new GameObject("Text");
		txtObj.transform.SetParent(btnObj.transform, false);
		RectTransform txtRect = txtObj.AddComponent<RectTransform>();
		txtRect.anchorMin = Vector2.zero;
		txtRect.anchorMax = Vector2.one;
		txtRect.sizeDelta = Vector2.zero;

		TMP_Text txt = txtObj.AddComponent<TextMeshProUGUI>();
		txt.text = "Upload Image";
		txt.fontSize = 12;
		txt.alignment = TextAlignmentOptions.Center;
		txt.color = Color.white;
	}

	void CreateGroupInfo() {
		GameObject txtObj = new GameObject("GroupText");
		txtObj.transform.SetParent(contentArea.transform, false);
		RectTransform txtRect = txtObj.AddComponent<RectTransform>();
		txtRect.anchorMin = Vector2.zero;
		txtRect.anchorMax = Vector2.one;
		txtRect.sizeDelta = Vector2.zero;

		TMP_Text txt = txtObj.AddComponent<TextMeshProUGUI>();
		txt.text = "Group: " + nodeData.groupName + "\n\nDrag nodes here to group them.";
		txt.fontSize = 14;
		txt.alignment = TextAlignmentOptions.Center;
		txt.color = Color.white;
	}

	TMP_Text CreateTextComponent(Transform parent, float fontSize = 14) {
		GameObject txtObj = new GameObject("Text");
		txtObj.transform.SetParent(parent, false);
		RectTransform txtRect = txtObj.AddComponent<RectTransform>();
		txtRect.anchorMin = Vector2.zero;
		txtRect.anchorMax = Vector2.one;
		txtRect.offsetMin = new Vector2(5, 5);
		txtRect.offsetMax = new Vector2(-5, -5);

		TMP_Text txt = txtObj.AddComponent<TextMeshProUGUI>();
		txt.fontSize = fontSize;
		txt.color = Color.black;
		return txt;
	}

	public void OnBeginDrag(PointerEventData eventData) {
		isDragging = true;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			rectTransform.parent as RectTransform,
			eventData.position,
			null,
			out dragOffset
		);
		dragOffset -= rectTransform.anchoredPosition;

		if(manager != null) {
			manager.StartConnection(this);
		}
	}

	public void OnDrag(PointerEventData eventData) {
		if(isDragging) {
			Vector2 localPoint;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(
				rectTransform.parent as RectTransform,
				eventData.position,
				null,
				out localPoint
			);

			rectTransform.anchoredPosition = localPoint - dragOffset;
			nodeData.position = rectTransform.anchoredPosition;
		}
	}

	public void OnEndDrag(PointerEventData eventData) {
		isDragging = false;
		manager.UpdateNodeData(nodeData);
	}

	public void OnPointerClick(PointerEventData eventData) {
		if(manager != null) {
			manager.EndConnection(this);
		}
	}
}