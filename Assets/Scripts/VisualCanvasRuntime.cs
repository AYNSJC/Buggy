using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[Serializable]
public class SubTaskInfo {
	public string subTaskName;
	public bool isCompleted;
}

[Serializable]
public class TaskInfo {
	public string taskName;
	public int urgency;
	public bool isCompleted;
	public bool isExpanded;
	public List<SubTaskInfo> subTasks = new List<SubTaskInfo>();
}

[Serializable]
public class ConnectionData {
	public string fromNodeId;
	public string toNodeId;
	public string connectionId;
}

[Serializable]
public class NodeData {
	public string nodeId;
	public string nodeType;
	public Vector2 position;
	public Vector2 size;
	public List<TaskInfo> tasks = new List<TaskInfo>();
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

public class VisualCanvasRuntime : MonoBehaviour {
	[HideInInspector] public RectTransform canvasContent;

	private CanvasData canvasData = new CanvasData();
	private Dictionary<string, VisualNodeRuntime> nodeObjects = new Dictionary<string, VisualNodeRuntime>();
	private Dictionary<string, GameObject> connectionObjects = new Dictionary<string, GameObject>();

	private bool isConnectionMode = false;
	private VisualNodeRuntime connectionStartNode = null;
	private GameObject currentDragLine = null;

	private Vector3 lastMousePosition;
	private bool isPanning = false;
	private string saveKey = "AutoVisualCanvasSaveData";

	private TMP_Text connectionModeText;

	void Start() {
		LoadCanvas();
		RefreshCanvas();
	}

	void Update() {
		HandlePanning();
		HandleZoom(); //VisualNodeRuntime

		if(isConnectionMode && currentDragLine != null) {
			UpdateDragLine();
		}

		foreach(var conn in canvasData.connections) {
			UpdateConnection(conn);
		}
	}

	void HandlePanning() {
		if(Input.GetMouseButtonDown(2)) {
			isPanning = true;
			lastMousePosition = Input.mousePosition;
		}

		if(isPanning && Input.GetMouseButton(2)) {
			Vector3 delta = Input.mousePosition - lastMousePosition;
			canvasContent.anchoredPosition += new Vector2(delta.x, delta.y) / canvasContent.localScale.x;
			canvasData.canvasOffset = canvasContent.anchoredPosition;
			lastMousePosition = Input.mousePosition;
		}

		if(Input.GetMouseButtonUp(2)) {
			isPanning = false;
		}
	}

	void HandleZoom() {
		float scroll = Input.mouseScrollDelta.y;
		if(scroll != 0) {
			float newZoom = Mathf.Clamp(canvasContent.localScale.x + scroll * 0.1f, 0.3f, 3f);
			canvasContent.localScale = Vector3.one * newZoom;
			canvasData.canvasZoom = newZoom;
		}
	}

	public void CreateNode(string nodeType) {
		Debug.Log("Creating node: " + nodeType);

		GameObject instructions = GameObject.Find("Instructions");
		if(instructions != null) {
			Destroy(instructions);
		}

		Vector2 spawnPos = new Vector2(UnityEngine.Random.Range(-500, 500), UnityEngine.Random.Range(-500, 500));

		NodeData newNode = new NodeData {
			nodeId = System.Guid.NewGuid().ToString(),
			nodeType = nodeType,
			position = spawnPos,
			size = new Vector2(350, 450)
		};

		if(nodeType == "todo") {
			newNode.tasks = new List<TaskInfo>();
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

		// SPLINE ARROW - Bezier curve
		Vector3 dir = (end - start);
		float dist = dir.magnitude;
		Vector3 controlPoint1 = start + Vector3.right * dist * 0.3f;
		Vector3 controlPoint2 = end - Vector3.right * dist * 0.3f;

		line.positionCount = 30;
		for(int i = 0; i < 30; i++) {
			float t = i / 29f;
			Vector3 point = CalculateBezierPoint(t, start, controlPoint1, controlPoint2, end);
			line.SetPosition(i, point);
		}
	}

	Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) {
		float u = 1 - t;
		float tt = t * t;
		float uu = u * u;
		float uuu = uu * u;
		float ttt = tt * t;

		Vector3 p = uuu * p0;
		p += 3 * uu * t * p1;
		p += 3 * u * tt * p2;
		p += ttt * p3;

		return p;
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

	public void ToggleConnectionMode() {
		isConnectionMode = !isConnectionMode;
		if(connectionModeText != null) {
			connectionModeText.text = isConnectionMode ? "[Connection: ON]" : "[Connection: OFF]";
		}
		CancelConnection();
		Debug.Log("Connection mode: " + isConnectionMode);
	}

	public void SetConnectionModeText(TMP_Text text) {
		connectionModeText = text;
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
	}

	void LoadCanvas() {
		if(PlayerPrefs.HasKey(saveKey)) {
			string json = PlayerPrefs.GetString(saveKey);
			canvasData = JsonUtility.FromJson<CanvasData>(json);
			if(canvasData == null) canvasData = new CanvasData();

			if(canvasContent != null) {
				canvasContent.anchoredPosition = canvasData.canvasOffset;
				canvasContent.localScale = Vector3.one * canvasData.canvasZoom;
			}
		}
	}

	public void ClearAll() {
		Debug.Log("Clearing all nodes!");
		canvasData = new CanvasData();
		SaveCanvas();
		RefreshCanvas();
	}
}

public class VisualNodeRuntime : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler {
	public NodeData nodeData;
	public VisualCanvasRuntime manager;
	private RectTransform rectTransform;
	private bool isDragging = false;
	private Vector2 dragOffset;

	private GameObject contentArea;
	private TMP_InputField groupNameInput; // For group editing

	public void Initialize(VisualCanvasRuntime mgr, NodeData data) {
		manager = mgr;
		nodeData = data;
		rectTransform = gameObject.AddComponent<RectTransform>();
		rectTransform.sizeDelta = data.size;
		rectTransform.anchoredPosition = data.position;

		BuildNodeUI();
	}

	void BuildNodeUI() {
		Image bg = gameObject.AddComponent<Image>();
		bg.color = nodeData.nodeColor;

		Shadow shadow = gameObject.AddComponent<Shadow>();
		shadow.effectDistance = new Vector2(3, -3);

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

		CreateDeleteButton(header.transform);
		CreateContentArea();
	}

	string GetNodeTitle() {
		switch(nodeData.nodeType) {
			case "todo": return "[TODO LIST]";
			case "text": return "[TEXT NOTE]";
			case "image": return "[IMAGE]";
			case "group": return "[GROUP]";
			default: return "[NODE]";
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
		txt.text = "X";
		txt.fontSize = 20;
		txt.fontStyle = FontStyles.Bold;
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

		if(nodeData.nodeType == "text") CreateTextEditor();
		else if(nodeData.nodeType == "todo") CreateTodoList();
		else if(nodeData.nodeType == "image") CreateImageViewer();
		else if(nodeData.nodeType == "group") CreateGroupEditor();
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

		CreateAddTaskButton(content.transform);

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
			TaskInfo newTask = new TaskInfo { taskName = "New Task", urgency = 0, isCompleted = false };
			nodeData.tasks.Add(newTask);
			manager.UpdateNodeData(nodeData);

			foreach(Transform child in contentArea.transform) {
				Destroy(child.gameObject);
			}
			CreateContentArea();
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

	void CreateTaskItem(Transform parent, TaskInfo task) {
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
		GameObject txtObj = new GameObject("ImagePlaceholder");
		txtObj.transform.SetParent(contentArea.transform, false);
		RectTransform txtRect = txtObj.AddComponent<RectTransform>();
		txtRect.anchorMin = Vector2.zero;
		txtRect.anchorMax = Vector2.one;
		txtRect.sizeDelta = Vector2.zero;

		TMP_Text txt = txtObj.AddComponent<TextMeshProUGUI>();
		txt.text = "IMAGE NODE\n\n(Upload feature coming soon)";
		txt.fontSize = 14;
		txt.alignment = TextAlignmentOptions.Center;
		txt.color = Color.white;
	}

	void CreateGroupEditor() {
		// Group Name Input
		GameObject nameInputObj = new GameObject("GroupNameInput");
		nameInputObj.transform.SetParent(contentArea.transform, false);
		RectTransform nameRect = nameInputObj.AddComponent<RectTransform>();
		nameRect.anchorMin = new Vector2(0, 1);
		nameRect.anchorMax = new Vector2(1, 1);
		nameRect.pivot = new Vector2(0.5f, 1);
		nameRect.sizeDelta = new Vector2(-20, 40);
		nameRect.anchoredPosition = new Vector2(0, -10);

		Image nameBg = nameInputObj.AddComponent<Image>();
		nameBg.color = new Color(1, 1, 1, 0.9f);

		groupNameInput = nameInputObj.AddComponent<TMP_InputField>();
		groupNameInput.textComponent = CreateTextComponent(nameInputObj.transform, 16);
		groupNameInput.text = nodeData.groupName;
		groupNameInput.onValueChanged.AddListener((val) => {
			nodeData.groupName = val;
			manager.UpdateNodeData(nodeData);
		});

		// Resize Handles
		CreateResizeHandles();

		// Info text
		GameObject infoObj = new GameObject("Info");
		infoObj.transform.SetParent(contentArea.transform, false);
		RectTransform infoRect = infoObj.AddComponent<RectTransform>();
		infoRect.anchorMin = new Vector2(0, 0);
		infoRect.anchorMax = new Vector2(1, 1);
		infoRect.offsetMin = new Vector2(10, 10);
		infoRect.offsetMax = new Vector2(-10, -60);

		TMP_Text infoText = infoObj.AddComponent<TextMeshProUGUI>();
		infoText.text = "Drag corners to resize\nDrag nodes here to group them";
		infoText.fontSize = 14;
		infoText.alignment = TextAlignmentOptions.Center;
		infoText.color = Color.white;
	}

	void CreateResizeHandles() {
		// Bottom-right resize handle
		GameObject handleObj = new GameObject("ResizeHandle");
		handleObj.transform.SetParent(transform, false);
		RectTransform handleRect = handleObj.AddComponent<RectTransform>();
		handleRect.anchorMin = new Vector2(1, 0);
		handleRect.anchorMax = new Vector2(1, 0);
		handleRect.pivot = new Vector2(1, 0);
		handleRect.sizeDelta = new Vector2(20, 20);
		handleRect.anchoredPosition = Vector2.zero;

		Image handleImg = handleObj.AddComponent<Image>();
		handleImg.color = new Color(1, 1, 1, 0.5f);

		ResizeHandle resizer = handleObj.AddComponent<ResizeHandle>();
		resizer.nodeRuntime = this;
	}

	public void ResizeNode(Vector2 sizeDelta) {
		rectTransform.sizeDelta += sizeDelta;
		nodeData.size = rectTransform.sizeDelta;
		manager.UpdateNodeData(nodeData);
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

// Resize handle for group nodes
public class ResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler {
	public VisualNodeRuntime nodeRuntime;
	private Vector2 startMousePos;
	private Vector2 startSize;

	public void OnBeginDrag(PointerEventData eventData) {
		startMousePos = eventData.position;
		startSize = nodeRuntime.nodeData.size;
	}

	public void OnDrag(PointerEventData eventData) {
		Vector2 delta = eventData.position - startMousePos;
		Vector2 newSize = startSize + delta;
		newSize.x = Mathf.Max(200, newSize.x);
		newSize.y = Mathf.Max(150, newSize.y);

		nodeRuntime.GetComponent<RectTransform>().sizeDelta = newSize;
		nodeRuntime.nodeData.size = newSize;
	}

	public void OnEndDrag(PointerEventData eventData) {
		nodeRuntime.manager.UpdateNodeData(nodeRuntime.nodeData);
	}
}

public class ButtonClickForwarder : MonoBehaviour {
	public VisualCanvasRuntime runtime;
	public string nodeType;
	public string actionType;

	void Start() {
		Button btn = GetComponent<Button>();
		if(btn != null) {
			btn.onClick.AddListener(OnClick);
		}
	}

	void OnClick() {
		if(runtime == null) {
			Debug.LogError("Runtime reference is null!");
			return;
		}

		Debug.Log("Button clicked: " + actionType);

		if(actionType == "createNode") {
			runtime.CreateNode(nodeType);
		}
		else if(actionType == "connection") {
			runtime.ToggleConnectionMode();
		}
		else if(actionType == "save") {
			PlayerPrefs.Save();
			Debug.Log("Canvas Saved!");
		}
		else if(actionType == "clear") {
			runtime.ClearAll();
		}
	}
}