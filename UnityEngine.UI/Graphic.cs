using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI.CoroutineTween;

namespace UnityEngine.UI
{
	/// <summary>
	///   <para>Base class for all visual UI Component.</para>
	/// </summary>
	[DisallowMultipleComponent, ExecuteInEditMode, RequireComponent(typeof(CanvasRenderer)), RequireComponent(typeof(RectTransform))]
	public abstract class Graphic : UIBehaviour, ICanvasElement
	{
		protected static Material s_DefaultUI = null;

		protected static Texture2D s_WhiteTexture = null;

		[FormerlySerializedAs("m_Mat"), SerializeField]
		protected Material m_Material;

		[SerializeField]
		private Color m_Color = Color.white;

		[SerializeField]
		private bool m_RaycastTarget = true;

		[NonSerialized]
		private RectTransform m_RectTransform;

		[NonSerialized]
		private CanvasRenderer m_CanvasRender;

		[NonSerialized]
		private Canvas m_Canvas;

		[NonSerialized]
		private bool m_VertsDirty;

		[NonSerialized]
		private bool m_MaterialDirty;

		[NonSerialized]
		protected UnityAction m_OnDirtyLayoutCallback;

		[NonSerialized]
		protected UnityAction m_OnDirtyVertsCallback;

		[NonSerialized]
		protected UnityAction m_OnDirtyMaterialCallback;

		[NonSerialized]
		protected static Mesh s_Mesh;

		[NonSerialized]
		private static readonly VertexHelper s_VertexHelper = new VertexHelper();

		[NonSerialized]
		private readonly TweenRunner<ColorTween> m_ColorTweenRunner;

		/// <summary>
		///   <para>Default material used to draw UI elements if no explicit material was specified.</para>
		/// </summary>
		public static Material defaultGraphicMaterial
		{
			get
			{
				if (Graphic.s_DefaultUI == null)
				{
					Graphic.s_DefaultUI = Canvas.GetDefaultCanvasMaterial();
				}
				return Graphic.s_DefaultUI;
			}
		}

		/// <summary>
		///   <para>Base color of the Graphic.</para>
		/// </summary>
		public Color color
		{
			get
			{
				return this.m_Color;
			}
			set
			{
				if (SetPropertyUtility.SetColor(ref this.m_Color, value))
				{
					this.SetVerticesDirty();
				}
			}
		}

		/// <summary>
		///   <para>Should this graphic be considered a target for raycasting?</para>
		/// </summary>
		public bool raycastTarget
		{
			get
			{
				return this.m_RaycastTarget;
			}
			set
			{
				this.m_RaycastTarget = value;
			}
		}

		protected bool useLegacyMeshGeneration
		{
			get;
			set;
		}

		/// <summary>
		///   <para>Absolute depth of the graphic in the hierarchy, used by rendering and events.</para>
		/// </summary>
		public int depth
		{
			get
			{
				return this.canvasRenderer.absoluteDepth;
			}
		}

		/// <summary>
		///   <para>The RectTransform component used by the Graphic.</para>
		/// </summary>
		public RectTransform rectTransform
		{
			get
			{
				RectTransform arg_1C_0;
				if ((arg_1C_0 = this.m_RectTransform) == null)
				{
					arg_1C_0 = (this.m_RectTransform = base.GetComponent<RectTransform>());
				}
				return arg_1C_0;
			}
		}

		/// <summary>
		///   <para>A reference to the Canvas this Graphic is rendering to.</para>
		/// </summary>
		public Canvas canvas
		{
			get
			{
				if (this.m_Canvas == null)
				{
					this.CacheCanvas();
				}
				return this.m_Canvas;
			}
		}

		/// <summary>
		///   <para>The CanvasRenderer used by this Graphic.</para>
		/// </summary>
		public CanvasRenderer canvasRenderer
		{
			get
			{
				if (this.m_CanvasRender == null)
				{
					this.m_CanvasRender = base.GetComponent<CanvasRenderer>();
				}
				return this.m_CanvasRender;
			}
		}

		/// <summary>
		///   <para>Returns the default material for the graphic.</para>
		/// </summary>
		public virtual Material defaultMaterial
		{
			get
			{
				return Graphic.defaultGraphicMaterial;
			}
		}

		/// <summary>
		///   <para>The Material set by the user.</para>
		/// </summary>
		public virtual Material material
		{
			get
			{
				return (!(this.m_Material != null)) ? this.defaultMaterial : this.m_Material;
			}
			set
			{
				if (this.m_Material == value)
				{
					return;
				}
				this.m_Material = value;
				this.SetMaterialDirty();
			}
		}

		/// <summary>
		///   <para>The material that will be sent for Rendering (Read only).</para>
		/// </summary>
		public virtual Material materialForRendering
		{
			get
			{
				List<Component> list = ListPool<Component>.Get();
				base.GetComponents(typeof(IMaterialModifier), list);
				Material material = this.material;
				for (int i = 0; i < list.Count; i++)
				{
					material = (list[i] as IMaterialModifier).GetModifiedMaterial(material);
				}
				ListPool<Component>.Release(list);
				return material;
			}
		}

		/// <summary>
		///   <para>The graphic's texture. (Read Only).</para>
		/// </summary>
		public virtual Texture mainTexture
		{
			get
			{
				return Graphic.s_WhiteTexture;
			}
		}

		protected static Mesh workerMesh
		{
			get
			{
				if (Graphic.s_Mesh == null)
				{
					Graphic.s_Mesh = new Mesh();
					Graphic.s_Mesh.name = "Shared UI Mesh";
					Graphic.s_Mesh.hideFlags = HideFlags.HideAndDontSave;
				}
				return Graphic.s_Mesh;
			}
		}

		protected Graphic()
		{
			if (this.m_ColorTweenRunner == null)
			{
				this.m_ColorTweenRunner = new TweenRunner<ColorTween>();
			}
			this.m_ColorTweenRunner.Init(this);
			this.useLegacyMeshGeneration = true;
		}

		/// <summary>
		///   <para>Mark the Graphic as dirty.</para>
		/// </summary>
		public virtual void SetAllDirty()
		{
			this.SetLayoutDirty();
			this.SetVerticesDirty();
			this.SetMaterialDirty();
		}

		/// <summary>
		///   <para>Mark the layout as dirty.</para>
		/// </summary>
		public virtual void SetLayoutDirty()
		{
			if (!this.IsActive())
			{
				return;
			}
			LayoutRebuilder.MarkLayoutForRebuild(this.rectTransform);
			if (this.m_OnDirtyLayoutCallback != null)
			{
				this.m_OnDirtyLayoutCallback();
			}
		}

		/// <summary>
		///   <para>Mark the vertices as dirty.</para>
		/// </summary>
		public virtual void SetVerticesDirty()
		{
			if (!this.IsActive())
			{
				return;
			}
			this.m_VertsDirty = true;
			CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
			if (this.m_OnDirtyVertsCallback != null)
			{
				this.m_OnDirtyVertsCallback();
			}
		}

		/// <summary>
		///   <para>Mark the Material as dirty.</para>
		/// </summary>
		public virtual void SetMaterialDirty()
		{
			if (!this.IsActive())
			{
				return;
			}
			this.m_MaterialDirty = true;
			CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
			if (this.m_OnDirtyMaterialCallback != null)
			{
				this.m_OnDirtyMaterialCallback();
			}
		}

		protected override void OnRectTransformDimensionsChange()
		{
			if (base.gameObject.activeInHierarchy)
			{
				if (CanvasUpdateRegistry.IsRebuildingLayout())
				{
					this.SetVerticesDirty();
				}
				else
				{
					this.SetVerticesDirty();
					this.SetLayoutDirty();
				}
			}
		}

		protected override void OnBeforeTransformParentChanged()
		{
			GraphicRegistry.UnregisterGraphicForCanvas(this.canvas, this);
			LayoutRebuilder.MarkLayoutForRebuild(this.rectTransform);
		}

		protected override void OnTransformParentChanged()
		{
			base.OnTransformParentChanged();
			if (!this.IsActive())
			{
				return;
			}
			this.CacheCanvas();
			GraphicRegistry.RegisterGraphicForCanvas(this.canvas, this);
			this.SetAllDirty();
		}

		private void CacheCanvas()
		{
			List<Canvas> list = ListPool<Canvas>.Get();
			base.gameObject.GetComponentsInParent<Canvas>(false, list);
			if (list.Count > 0)
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i].isActiveAndEnabled)
					{
						this.m_Canvas = list[i];
						break;
					}
				}
			}
			else
			{
				this.m_Canvas = null;
			}
			ListPool<Canvas>.Release(list);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			this.CacheCanvas();
			GraphicRegistry.RegisterGraphicForCanvas(this.canvas, this);
			GraphicRebuildTracker.TrackGraphic(this);
			if (Graphic.s_WhiteTexture == null)
			{
				Graphic.s_WhiteTexture = Texture2D.whiteTexture;
			}
			this.SetAllDirty();
		}

		/// <summary>
		///   <para>See MonoBehaviour.OnDisable.</para>
		/// </summary>
		protected override void OnDisable()
		{
			GraphicRebuildTracker.UnTrackGraphic(this);
			GraphicRegistry.UnregisterGraphicForCanvas(this.canvas, this);
			CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
			if (this.canvasRenderer != null)
			{
				this.canvasRenderer.Clear();
			}
			LayoutRebuilder.MarkLayoutForRebuild(this.rectTransform);
			base.OnDisable();
		}

		protected override void OnCanvasHierarchyChanged()
		{
			Canvas canvas = this.m_Canvas;
			this.CacheCanvas();
			if (canvas != this.m_Canvas)
			{
				GraphicRegistry.UnregisterGraphicForCanvas(canvas, this);
				GraphicRegistry.RegisterGraphicForCanvas(this.canvas, this);
			}
		}

		/// <summary>
		///   <para>Rebuilds the graphic geometry and its material on the PreRender cycle.</para>
		/// </summary>
		/// <param name="update">The current step of the rendering CanvasUpdate cycle.</param>
		public virtual void Rebuild(CanvasUpdate update)
		{
			if (this.canvasRenderer.cull)
			{
				return;
			}
			if (update == CanvasUpdate.PreRender)
			{
				if (this.m_VertsDirty)
				{
					this.UpdateGeometry();
					this.m_VertsDirty = false;
				}
				if (this.m_MaterialDirty)
				{
					this.UpdateMaterial();
					this.m_MaterialDirty = false;
				}
			}
		}

		/// <summary>
		///   <para>See ICanvasElement.LayoutComplete.</para>
		/// </summary>
		public virtual void LayoutComplete()
		{
		}

		/// <summary>
		///   <para>See ICanvasElement.GraphicUpdateComplete.</para>
		/// </summary>
		public virtual void GraphicUpdateComplete()
		{
		}

		/// <summary>
		///   <para>Call to update the Material of the graphic onto the CanvasRenderer.</para>
		/// </summary>
		protected virtual void UpdateMaterial()
		{
			if (!this.IsActive())
			{
				return;
			}
			this.canvasRenderer.materialCount = 1;
			this.canvasRenderer.SetMaterial(this.materialForRendering, 0);
			this.canvasRenderer.SetTexture(this.mainTexture);
		}

		/// <summary>
		///   <para>Call to update the geometry of the Graphic onto the CanvasRenderer.</para>
		/// </summary>
		protected virtual void UpdateGeometry()
		{
			if (this.useLegacyMeshGeneration)
			{
				this.DoLegacyMeshGeneration();
			}
			else
			{
				this.DoMeshGeneration();
			}
		}

		private void DoMeshGeneration()
		{
			if (this.rectTransform != null && this.rectTransform.rect.width >= 0f && this.rectTransform.rect.height >= 0f)
			{
				this.OnPopulateMesh(Graphic.s_VertexHelper);
			}
			else
			{
				Graphic.s_VertexHelper.Clear();
			}
			List<Component> list = ListPool<Component>.Get();
			base.GetComponents(typeof(IMeshModifier), list);
			for (int i = 0; i < list.Count; i++)
			{
				((IMeshModifier)list[i]).ModifyMesh(Graphic.s_VertexHelper);
			}
			ListPool<Component>.Release(list);
			Graphic.s_VertexHelper.FillMesh(Graphic.workerMesh);
			this.canvasRenderer.SetMesh(Graphic.workerMesh);
		}

		private void DoLegacyMeshGeneration()
		{
			if (this.rectTransform != null && this.rectTransform.rect.width >= 0f && this.rectTransform.rect.height >= 0f)
			{
				this.OnPopulateMesh(Graphic.workerMesh);
			}
			else
			{
				Graphic.workerMesh.Clear();
			}
			List<Component> list = ListPool<Component>.Get();
			base.GetComponents(typeof(IMeshModifier), list);
			for (int i = 0; i < list.Count; i++)
			{
				((IMeshModifier)list[i]).ModifyMesh(Graphic.workerMesh);
			}
			ListPool<Component>.Release(list);
			this.canvasRenderer.SetMesh(Graphic.workerMesh);
		}

		[Obsolete("Use OnPopulateMesh instead.", true)]
		protected virtual void OnFillVBO(List<UIVertex> vbo)
		{
		}

		/// <summary>
		///   <para>Callback function when a UI element needs to generate vertices.</para>
		/// </summary>
		/// <param name="m">Mesh to populate with UI data.</param>
		/// <param name="vh"></param>
		[Obsolete("Use OnPopulateMesh(VertexHelper vh) instead.", false)]
		protected virtual void OnPopulateMesh(Mesh m)
		{
			this.OnPopulateMesh(Graphic.s_VertexHelper);
			Graphic.s_VertexHelper.FillMesh(m);
		}

		/// <summary>
		///   <para>Callback function when a UI element needs to generate vertices.</para>
		/// </summary>
		/// <param name="m">Mesh to populate with UI data.</param>
		/// <param name="vh"></param>
		protected virtual void OnPopulateMesh(VertexHelper vh)
		{
			Rect pixelAdjustedRect = this.GetPixelAdjustedRect();
			Vector4 vector = new Vector4(pixelAdjustedRect.x, pixelAdjustedRect.y, pixelAdjustedRect.x + pixelAdjustedRect.width, pixelAdjustedRect.y + pixelAdjustedRect.height);
			Color32 color = this.color;
			vh.Clear();
			vh.AddVert(new Vector3(vector.x, vector.y), color, new Vector2(0f, 0f));
			vh.AddVert(new Vector3(vector.x, vector.w), color, new Vector2(0f, 1f));
			vh.AddVert(new Vector3(vector.z, vector.w), color, new Vector2(1f, 1f));
			vh.AddVert(new Vector3(vector.z, vector.y), color, new Vector2(1f, 0f));
			vh.AddTriangle(0, 1, 2);
			vh.AddTriangle(2, 3, 0);
		}

		/// <summary>
		///   <para>Editor-only callback that is issued by Unity if a rebuild of the Graphic is required.</para>
		/// </summary>
		public virtual void OnRebuildRequested()
		{
			MonoBehaviour[] components = base.gameObject.GetComponents<MonoBehaviour>();
			MonoBehaviour[] array = components;
			for (int i = 0; i < array.Length; i++)
			{
				MonoBehaviour monoBehaviour = array[i];
				MethodInfo method = monoBehaviour.GetType().GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					method.Invoke(monoBehaviour, null);
				}
			}
		}

		protected override void OnDidApplyAnimationProperties()
		{
			this.SetAllDirty();
		}

		/// <summary>
		///   <para>Adjusts the graphic size to make it pixel-perfect.</para>
		/// </summary>
		public virtual void SetNativeSize()
		{
		}

		/// <summary>
		///   <para>When a GraphicRaycaster is raycasting into the scene it will first filter the elments using their RectTransform rect and then it will use this function to determine the elements hit mby the raycast.</para>
		/// </summary>
		/// <param name="sp">Screen point.</param>
		/// <param name="eventCamera">Camera.</param>
		/// <returns>
		///   <para>True if the provided point is a valid location for GraphicRaycaster raycasts.</para>
		/// </returns>
		public virtual bool Raycast(Vector2 sp, Camera eventCamera)
		{
			if (!base.isActiveAndEnabled)
			{
				return false;
			}
			Transform transform = base.transform;
			List<Component> list = ListPool<Component>.Get();
			bool flag = false;
			while (transform != null)
			{
				transform.GetComponents<Component>(list);
				for (int i = 0; i < list.Count; i++)
				{
					ICanvasRaycastFilter canvasRaycastFilter = list[i] as ICanvasRaycastFilter;
					if (canvasRaycastFilter != null)
					{
						bool flag2 = true;
						CanvasGroup canvasGroup = list[i] as CanvasGroup;
						if (canvasGroup != null)
						{
							if (!flag && canvasGroup.ignoreParentGroups)
							{
								flag = true;
								flag2 = canvasRaycastFilter.IsRaycastLocationValid(sp, eventCamera);
							}
							else if (!flag)
							{
								flag2 = canvasRaycastFilter.IsRaycastLocationValid(sp, eventCamera);
							}
						}
						else
						{
							flag2 = canvasRaycastFilter.IsRaycastLocationValid(sp, eventCamera);
						}
						if (!flag2)
						{
							ListPool<Component>.Release(list);
							return false;
						}
					}
				}
				transform = transform.parent;
			}
			ListPool<Component>.Release(list);
			return true;
		}

		protected override void OnValidate()
		{
			base.OnValidate();
			this.SetAllDirty();
		}

		/// <summary>
		///   <para>Adjusts the given pixel to be pixel perfect.</para>
		/// </summary>
		/// <param name="point">Local space point.</param>
		/// <returns>
		///   <para>Pixel perfect adjusted point.</para>
		/// </returns>
		public Vector2 PixelAdjustPoint(Vector2 point)
		{
			if (!this.canvas || !this.canvas.pixelPerfect)
			{
				return point;
			}
			return RectTransformUtility.PixelAdjustPoint(point, base.transform, this.canvas);
		}

		/// <summary>
		///   <para>Returns a pixel perfect Rect closest to the Graphic RectTransform.</para>
		/// </summary>
		/// <returns>
		///   <para>Pixel perfect Rect.</para>
		/// </returns>
		public Rect GetPixelAdjustedRect()
		{
			if (!this.canvas || !this.canvas.pixelPerfect)
			{
				return this.rectTransform.rect;
			}
			return RectTransformUtility.PixelAdjustRect(this.rectTransform, this.canvas);
		}

		/// <summary>
		///   <para>Tweens the CanvasRenderer color associated with this Graphic.</para>
		/// </summary>
		/// <param name="targetColor">Target color.</param>
		/// <param name="duration">Tween duration.</param>
		/// <param name="ignoreTimeScale">Should ignore Time.scale?</param>
		/// <param name="useAlpha">Should also Tween the alpha channel?</param>
		public void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha)
		{
			this.CrossFadeColor(targetColor, duration, ignoreTimeScale, useAlpha, true);
		}

		private void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha, bool useRGB)
		{
			if (this.canvasRenderer == null || (!useRGB && !useAlpha))
			{
				return;
			}
			if (this.canvasRenderer.GetColor().Equals(targetColor))
			{
				return;
			}
			ColorTween.ColorTweenMode tweenMode = (!useRGB || !useAlpha) ? ((!useRGB) ? ColorTween.ColorTweenMode.Alpha : ColorTween.ColorTweenMode.RGB) : ColorTween.ColorTweenMode.All;
			ColorTween info = new ColorTween
			{
				duration = duration,
				startColor = this.canvasRenderer.GetColor(),
				targetColor = targetColor
			};
			info.AddOnChangedCallback(new UnityAction<Color>(this.canvasRenderer.SetColor));
			info.ignoreTimeScale = ignoreTimeScale;
			info.tweenMode = tweenMode;
			this.m_ColorTweenRunner.StartTween(info);
		}

		private static Color CreateColorFromAlpha(float alpha)
		{
			Color black = Color.black;
			black.a = alpha;
			return black;
		}

		/// <summary>
		///   <para>Tweens the alpha of the CanvasRenderer color associated with this Graphic.</para>
		/// </summary>
		/// <param name="alpha">Target alpha.</param>
		/// <param name="duration">Duration of the tween in seconds.</param>
		/// <param name="ignoreTimeScale">Should ignore Time.scale?</param>
		public void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale)
		{
			this.CrossFadeColor(Graphic.CreateColorFromAlpha(alpha), duration, ignoreTimeScale, true, false);
		}

		/// <summary>
		///   <para>Add a listener to receive notification when the graphics layout is dirtied.</para>
		/// </summary>
		/// <param name="action"></param>
		public void RegisterDirtyLayoutCallback(UnityAction action)
		{
			this.m_OnDirtyLayoutCallback = (UnityAction)Delegate.Combine(this.m_OnDirtyLayoutCallback, action);
		}

		/// <summary>
		///   <para>Remove a listener from receiving notifications when the graphics layout is dirtied.</para>
		/// </summary>
		/// <param name="action"></param>
		public void UnregisterDirtyLayoutCallback(UnityAction action)
		{
			this.m_OnDirtyLayoutCallback = (UnityAction)Delegate.Remove(this.m_OnDirtyLayoutCallback, action);
		}

		/// <summary>
		///   <para>Add a listener to receive notification when the graphics vertices are dirtied.</para>
		/// </summary>
		/// <param name="action"></param>
		public void RegisterDirtyVerticesCallback(UnityAction action)
		{
			this.m_OnDirtyVertsCallback = (UnityAction)Delegate.Combine(this.m_OnDirtyVertsCallback, action);
		}

		/// <summary>
		///   <para>Remove a listener from receiving notifications when the graphics vertices are dirtied.</para>
		/// </summary>
		/// <param name="action">The delegate function to remove.</param>
		public void UnregisterDirtyVerticesCallback(UnityAction action)
		{
			this.m_OnDirtyVertsCallback = (UnityAction)Delegate.Remove(this.m_OnDirtyVertsCallback, action);
		}

		/// <summary>
		///   <para>Add a listener to receive notification when the graphics material is dirtied.</para>
		/// </summary>
		/// <param name="action"></param>
		public void RegisterDirtyMaterialCallback(UnityAction action)
		{
			this.m_OnDirtyMaterialCallback = (UnityAction)Delegate.Combine(this.m_OnDirtyMaterialCallback, action);
		}

		/// <summary>
		///   <para>Remove a listener from receiving notifications when the graphics material is dirtied.</para>
		/// </summary>
		/// <param name="action"></param>
		public void UnregisterDirtyMaterialCallback(UnityAction action)
		{
			this.m_OnDirtyMaterialCallback = (UnityAction)Delegate.Remove(this.m_OnDirtyMaterialCallback, action);
		}

		virtual bool IsDestroyed()
		{
			return base.IsDestroyed();
		}

		virtual Transform get_transform()
		{
			return base.transform;
		}
	}
}