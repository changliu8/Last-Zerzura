using UnityEngine;
using UnityEngine.Experimental.U2D.Common;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using Unity.Collections;

namespace UnityEngine.Experimental.U2D.Animation
{
    [Preserve]
    [ExecuteInEditMode]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteSkin : MonoBehaviour
    {
        [SerializeField]
        private Transform m_RootBone;
        [SerializeField]
        private Transform[] m_BoneTransforms;
        [SerializeField]
        private Bounds m_Bounds;
        private SpriteRenderer m_SpriteRenderer;
        private Sprite m_CurrentSprite;

#if UNITY_EDITOR
        internal static Events.UnityEvent onDrawGizmos = new Events.UnityEvent();
        private void OnDrawGizmos() { onDrawGizmos.Invoke(); }
#endif

        internal SpriteRenderer spriteRenderer
        {
            get
            {
                if (m_SpriteRenderer == null)
                    m_SpriteRenderer = GetComponent<SpriteRenderer>();
                return m_SpriteRenderer;
            }
        }

        internal Transform[] boneTransforms
        {
            get { return m_BoneTransforms; }
            set { m_BoneTransforms = value; }
        }

        internal Transform rootBone
        {
            get { return m_RootBone; }
            set { m_RootBone = value; }
        }

        internal Bounds bounds
        {
            get { return m_Bounds; }
            set
            {
                m_Bounds = value;
                UpdateBounds();
            }
        }

        internal bool isValid
        {
            get { return this.Validate() == SpriteSkinValidationResult.Ready; }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            ValidateTransformArray();
        }

        private void ValidateTransformArray()
        {
            if (spriteRenderer.sprite)
            {
                var bindPose = spriteRenderer.sprite.GetBindPoses();

                if (m_BoneTransforms == null || m_BoneTransforms.Length != bindPose.Length)
                    m_BoneTransforms = new Transform[bindPose.Length];
            }
        }
#endif
        private void OnEnable()
        {
            UpdateBounds();
        }

        private void OnDisable()
        {
            DeactivateSkinning();
        }

        internal void UpdateBounds()
        {
            if (!isValid)
                return;

            var matrix = transform.worldToLocalMatrix * m_RootBone.localToWorldMatrix;
            var extents = m_Bounds.extents;
            var p0 = matrix.MultiplyPoint3x4(m_Bounds.center + new Vector3(-extents.x, -extents.y, extents.z));
            var p1 = matrix.MultiplyPoint3x4(m_Bounds.center + new Vector3(-extents.x, extents.y, extents.z));
            var p2 = matrix.MultiplyPoint3x4(m_Bounds.center + extents);
            var p3 = matrix.MultiplyPoint3x4(m_Bounds.center + new Vector3(extents.x, -extents.y, extents.z));
            var bounds = new Bounds();
            bounds.min = p0;
            bounds.max = p0;
            bounds.Encapsulate(p1);
            bounds.Encapsulate(p2);
            bounds.Encapsulate(p3);
            InternalEngineBridge.SetLocalAABB(spriteRenderer, bounds);
        }

        private void DeactivateSkinning()
        {
            SpriteRendererDataAccessExtensions.DeactivateDeformableBuffer(spriteRenderer);

            if (spriteRenderer.sprite != null)
                InternalEngineBridge.SetLocalAABB(spriteRenderer, spriteRenderer.sprite.bounds);
        }

        private void LateUpdate()
        {
            if (m_CurrentSprite != spriteRenderer.sprite)
            {
                m_CurrentSprite = spriteRenderer.sprite;
                DeactivateSkinning();
            }

            if (!spriteRenderer.enabled || !isValid)
            {
                DeactivateSkinning();
                return;
            }

            UpdateBounds();

            var bindPoses = m_CurrentSprite.GetBindPoses();
            var boneWeights = m_CurrentSprite.GetBoneWeights();
            var outputVertices = spriteRenderer.GetDeformableVertices();
            var transformMatrices = new NativeArray<Matrix4x4>(m_BoneTransforms.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < m_BoneTransforms.Length; ++i)
                transformMatrices[i] = m_BoneTransforms[i].localToWorldMatrix;

            var deformJobHandle = SpriteSkinUtility.Deform(m_CurrentSprite.GetVertexAttribute<Vector3>(VertexAttribute.Position), boneWeights, transform.worldToLocalMatrix, bindPoses, transformMatrices, outputVertices);

            spriteRenderer.UpdateDeformableBuffer(deformJobHandle);
        }
    }
}
