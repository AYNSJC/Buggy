#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEditor;

public class VisualCanvasEditor : EditorWindow {

	[MenuItem("Tools/Create Visual Canvas System")]
	static void CreateVisualCanvas() {
		// Delete old one if exists
		GameObject oldCanvas = GameObject.Find("VisualCanvas");
		if(oldCanvas != null) {
			DestroyImmediate(oldCanvas);
		}

		// Check if EventSystem exists
		EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
		if(eventSystem == null) {
			GameObject eventSystemObj = new GameObject("EventSystem");
			eventSystemObj.AddComponent<EventSystem>();
			eventSystemObj.AddComponent<StandaloneInputModule>();
		}

		// Create main canvas GameObject
		GameObject canvasObj = new GameObject("VisualCanvas");
		Canvas canvas = canvasObj.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 0;

		CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;

		canvasObj.AddComponent<GraphicRaycaster>();

		// Create background/content area FIRST
		GameObject contentObj = new GameObject("CanvasContent");
		contentObj.transform.SetParent(canvasObj.transform, false);
		RectTransform contentRect = contentObj.AddComponent<RectTransform>();
		contentRect.anchorMin = new Vector2(0.5f, 0.5f);
		contentRect.anchorMax = new Vector2(0.5f, 0.5f);
		contentRect.pivot = new Vector2(0.5f, 0.5f);
		contentRect.sizeDelta = new Vector2(10000, 10000);
		contentRect.anchoredPosition = Vector2.zero;
		contentRect.localScale = Vector3.one;

		Image contentBg = contentObj.AddComponent<Image>();
		contentBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
		contentBg.raycastTarget = false;

		// Add runtime script AFTER content is created
		VisualCanvasRuntime runtime = canvasObj.AddComponent<VisualCanvasRuntime>();
		runtime.canvasContent = contentRect;

		// Create toolbar
		CreateToolbar(canvasObj.transform, runtime);

		// Create instruction text
		CreateInstructionText(canvasObj.transform);

		// Force save the scene
		UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

		Debug.Log("=== VISUAL CANVAS CREATED ===");
		Debug.Log("PRESS PLAY NOW!");

		Selection.activeGameObject = canvasObj;
		EditorGUIUtility.PingObject(canvasObj);
	}

	static void CreateInstructionText(Transform parent) {
		GameObject instructionObj = new GameObject("Instructions");
		instructionObj.transform.SetParent(parent, false);
		RectTransform instructRect = instructionObj.AddComponent<RectTransform>();
		instructRect.anchorMin = new Vector2(0.5f, 0.5f);
		instructRect.anchorMax = new Vector2(0.5f, 0.5f);
		instructRect.pivot = new Vector2(0.5f, 0.5f);
		instructRect.sizeDelta = new Vector2(700, 500);
		instructRect.anchoredPosition = Vector2.zero;

		Image bg = instructionObj.AddComponent<Image>();
		bg.color = new Color(0.2f, 0.3f, 0.4f, 0.95f);

		Shadow shadow = instructionObj.AddComponent<Shadow>();
		shadow.effectDistance = new Vector2(5, -5);
		shadow.effectColor = new Color(0, 0, 0, 0.5f);

		GameObject textObj = new GameObject("Text");
		textObj.transform.SetParent(instructionObj.transform, false);
		RectTransform textRect = textObj.AddComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(20, 20);
		textRect.offsetMax = new Vector2(-20, -20);

		TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
		text.text = @"<size=24><b>VISUAL CANVAS SYSTEM</b></size>

<size=18><b>CONTROLS:</b></size>

<b>Middle Mouse Button</b> = Pan Canvas
<b>Mouse Scroll</b> = Zoom In/Out
<b>Click Toolbar Buttons</b> = Add Nodes
<b>Drag Nodes</b> = Move Them Around


<size=18><b>TRY IT:</b></size>

Click the <b>'Add Todo'</b> button above!


<size=14><i>This message will disappear when you add your first node</i></size>";
		text.fontSize = 16;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.TopLeft;
		text.enableWordWrapping = true;
	}

	static GameObject CreateToolbar(Transform parent, VisualCanvasRuntime runtime) {
		GameObject toolbar = new GameObject("Toolbar");
		toolbar.transform.SetParent(parent, false);
		RectTransform toolbarRect = toolbar.AddComponent<RectTransform>();
		toolbarRect.anchorMin = new Vector2(0, 1);
		toolbarRect.anchorMax = new Vector2(1, 1);
		toolbarRect.pivot = new Vector2(0.5f, 1);
		toolbarRect.sizeDelta = new Vector2(0, 70);
		toolbarRect.anchoredPosition = Vector2.zero;

		Image toolbarBg = toolbar.AddComponent<Image>();
		toolbarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

		HorizontalLayoutGroup layout = toolbar.AddComponent<HorizontalLayoutGroup>();
		layout.padding = new RectOffset(15, 15, 15, 15);
		layout.spacing = 12;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = true;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childAlignment = TextAnchor.MiddleLeft;

		// Create buttons with explicit listeners
		CreateButtonWithAction(toolbar.transform, "Add Todo", runtime, "todo");
		CreateButtonWithAction(toolbar.transform, "Add Text", runtime, "text");
		CreateButtonWithAction(toolbar.transform, "Add Image", runtime, "image");
		CreateButtonWithAction(toolbar.transform, "Add Group", runtime, "group");

		// Spacer
		GameObject spacer = new GameObject("Spacer");
		spacer.transform.SetParent(toolbar.transform, false);
		LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
		spacerLayout.preferredWidth = 30;

		// Connection button
		GameObject connBtn = CreateButtonSimple(toolbar.transform, "[Connection: OFF]");
		Button connButton = connBtn.GetComponent<Button>();
		TMP_Text connText = connBtn.GetComponentInChildren<TMP_Text>();

		// Store reference for runtime
		runtime.SetConnectionModeText(connText);

		// Add ButtonClickForwarder component
		ButtonClickForwarder connForwarder = connBtn.AddComponent<ButtonClickForwarder>();
		connForwarder.runtime = runtime;
		connForwarder.actionType = "connection";

		// Save and Clear buttons
		GameObject saveBtn = CreateButtonSimple(toolbar.transform, "Save");
		ButtonClickForwarder saveForwarder = saveBtn.AddComponent<ButtonClickForwarder>();
		saveForwarder.runtime = runtime;
		saveForwarder.actionType = "save";

		GameObject clearBtn = CreateButtonSimple(toolbar.transform, "Clear All");
		ButtonClickForwarder clearForwarder = clearBtn.AddComponent<ButtonClickForwarder>();
		clearForwarder.runtime = runtime;
		clearForwarder.actionType = "clear";

		return toolbar;
	}

	static void CreateButtonWithAction(Transform parent, string text, VisualCanvasRuntime runtime, string nodeType) {
		GameObject btnObj = CreateButtonSimple(parent, text);

		// Add the forwarder component
		ButtonClickForwarder forwarder = btnObj.AddComponent<ButtonClickForwarder>();
		forwarder.runtime = runtime;
		forwarder.nodeType = nodeType;
		forwarder.actionType = "createNode";
	}

	static GameObject CreateButtonSimple(Transform parent, string text) {
		GameObject btnObj = new GameObject("Btn_" + text.Replace(" ", ""));
		btnObj.transform.SetParent(parent, false);

		LayoutElement layoutElement = btnObj.AddComponent<LayoutElement>();
		layoutElement.preferredWidth = 160;
		layoutElement.preferredHeight = 45;

		Image img = btnObj.AddComponent<Image>();
		img.color = new Color(0.25f, 0.45f, 0.65f, 1f);

		Button btn = btnObj.AddComponent<Button>();

		ColorBlock colors = btn.colors;
		colors.normalColor = new Color(0.25f, 0.45f, 0.65f, 1f);
		colors.highlightedColor = new Color(0.35f, 0.55f, 0.75f, 1f);
		colors.pressedColor = new Color(0.15f, 0.35f, 0.55f, 1f);
		btn.colors = colors;

		Shadow shadow = btnObj.AddComponent<Shadow>();
		shadow.effectDistance = new Vector2(2, -2);
		shadow.effectColor = new Color(0, 0, 0, 0.5f);

		GameObject textObj = new GameObject("Text");
		textObj.transform.SetParent(btnObj.transform, false);

		RectTransform textRect = textObj.AddComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.sizeDelta = Vector2.zero;

		TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
		tmp.text = text;
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.fontSize = 15;
		tmp.color = Color.white;
		tmp.fontStyle = FontStyles.Bold;

		return btnObj;
	}
}

// Helper component to forward button clicks at runtime
public class ButtonClickForwarder : MonoBehaviour {
	public VisualCanvasRuntime runtime;
	public string nodeType;
	public string actionType; // "createNode", "connection", "save", "clear"

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
#endif