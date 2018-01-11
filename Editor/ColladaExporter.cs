using UnityEngine;
using UnityEditor;
using System;
using System.Xml;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/**
 * Collada Exporter
 *
 * @author      Hiroki Omae
 * @author      Michael Grenier
 * @author_url  http://mgrenier.me
 * @copyright   2017 (c) Hiroki Omae
 * @copyright   2011 (c) Michael Grenier
 * @license     MIT - http://www.opensource.org/licenses/MIT
 */
public class ColladaExporter : IDisposable
{
    private class TransformAnimationCurves {
        public AnimationCurve positionX;
        public AnimationCurve positionY;
        public AnimationCurve positionZ;

        public AnimationCurve rotationX;
        public AnimationCurve rotationY;
        public AnimationCurve rotationZ;
        public AnimationCurve rotationW;

        public AnimationCurve scaleX;
        public AnimationCurve scaleY;
        public AnimationCurve scaleZ;

        public Matrix4x4 CreateTransformMatrix(int i) {
            var m = new Matrix4x4 ();

            //convert right-hand system/left-hand system (to-from Collada(right) <-> Unity(left))
            var position = new Vector3 (-positionX.keys[i].value, positionY.keys[i].value, positionZ.keys[i].value);
            var rotation = new Quaternion (-rotationX.keys[i].value, rotationY.keys[i].value, rotationZ.keys[i].value, -rotationW.keys[i].value);
            var scale    = new Vector3 (scaleX.keys[i].value, scaleY.keys[i].value, scaleZ.keys[i].value);

            m.SetTRS (position, rotation, scale);

            return m;
        }
    }

    protected string path;
    public const string COLLADA = "http://www.collada.org/2005/11/COLLADASchema";
    public XmlDocument xml
    {
        get;
        protected set;
    }
    public XmlNamespaceManager nsManager
    {
        get;
        protected set;
    }

    protected XmlNode root;
    protected XmlNode cameras;
    protected XmlNode lights;
    protected XmlNode images;
    protected XmlNode effects;
    protected XmlNode materials;
    protected XmlNode geometries;
    protected XmlNode animations;
    protected XmlNode animation_clips;
    protected XmlNode controllers;
    protected XmlNode visual_scenes;
    protected XmlNode default_scene;
    protected XmlNode scene;

    public ColladaExporter(String path)
    : this(path, true)
    {
    }

    public ColladaExporter(String path, bool replace)
    {
        this.path = path;
        this.xml = new XmlDocument();

        this.nsManager = new XmlNamespaceManager(this.xml.NameTable);
        this.nsManager.AddNamespace("x", COLLADA);

        if (!replace)
        {
            try
            {
                XmlTextReader reader = new XmlTextReader(path);
                this.xml.Load(reader);
                reader.Close();
                reader = null;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }
        else
            this.xml.AppendChild(this.xml.CreateXmlDeclaration("1.0", "UTF-8", null));

        XmlAttribute attr;

        this.root = this.xml.SelectSingleNode("/x:COLLADA", this.nsManager);
        if (this.root == null)
        {
            this.root = this.xml.AppendChild(this.xml.CreateElement("COLLADA", COLLADA));
            attr = this.xml.CreateAttribute("version");
            attr.Value = "1.4.1";
            this.root.Attributes.Append(attr);
        }

        XmlNode node;

        // Create asset
        {
            node = this.root.SelectSingleNode("/x:asset", this.nsManager);
            if (node == null)
            {
                this.root
                .AppendChild(
                    this.xml.CreateElement("asset", COLLADA)
                    .AppendChild(
                        this.xml.CreateElement("contributor", COLLADA)
                        .AppendChild(
                            this.xml.CreateElement("author", COLLADA)
                            .AppendChild(this.xml.CreateTextNode("Unity3D User"))
                            .ParentNode
                        )
                        .ParentNode
                        .AppendChild(
                            this.xml.CreateElement("author_tool", COLLADA)
                            .AppendChild(this.xml.CreateTextNode("Unity " + Application.unityVersion))
                            .ParentNode
                        )
                        .ParentNode
                    )
                    .ParentNode
                    .AppendChild(
                        this.xml.CreateElement("created", COLLADA)
                        .AppendChild(this.xml.CreateTextNode(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:00")))
                        .ParentNode
                    )
                    .ParentNode
                    .AppendChild(
                        this.xml.CreateElement("modified", COLLADA)
                        .AppendChild(this.xml.CreateTextNode(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:00")))
                        .ParentNode
                    )
                    .ParentNode
                    .AppendChild(
                        this.xml.CreateElement("up_axis", COLLADA)
                        .AppendChild(this.xml.CreateTextNode("Y_UP"))
                        .ParentNode
                    )
                    .ParentNode
                );
            }
        }

        // Create libraries
        this.cameras = this.root.SelectSingleNode("/x:library_cameras", this.nsManager);
        if (this.cameras == null)
            this.cameras = this.root.AppendChild(this.xml.CreateElement("library_cameras", COLLADA));
        this.lights = this.root.SelectSingleNode("/x:library_lights", this.nsManager);
        if (this.lights == null)
            this.lights = this.root.AppendChild(this.xml.CreateElement("library_lights", COLLADA));
        this.images = this.root.SelectSingleNode("/x:library_images", this.nsManager);
        if (this.images == null)
            this.images = this.root.AppendChild(this.xml.CreateElement("library_images", COLLADA));
        this.effects = this.root.SelectSingleNode("/x:library_effects", this.nsManager);
        if (this.effects == null)
            this.effects = this.root.AppendChild(this.xml.CreateElement("library_effects", COLLADA));
        this.materials = this.root.SelectSingleNode("/x:library_materials", this.nsManager);
        if (this.materials == null)
            this.materials = this.root.AppendChild(this.xml.CreateElement("library_materials", COLLADA));
        this.geometries = this.root.SelectSingleNode("/x:library_geometries", this.nsManager);
        if (this.geometries == null)
            this.geometries = this.root.AppendChild(this.xml.CreateElement("library_geometries", COLLADA));
        this.animations = this.root.SelectSingleNode("/x:library_animations", this.nsManager);
        if (this.animations == null)
            this.animations = this.root.AppendChild(this.xml.CreateElement("library_animations", COLLADA));
        this.animation_clips = this.root.SelectSingleNode("/x:library_animation_clips", this.nsManager);
        if (this.animation_clips == null)
            this.animation_clips = this.root.AppendChild(this.xml.CreateElement("library_animation_clips", COLLADA));
        this.controllers = this.root.SelectSingleNode("/x:library_controllers", this.nsManager);
        if (this.controllers == null)
            this.controllers = this.root.AppendChild(this.xml.CreateElement("library_controllers", COLLADA));
        this.visual_scenes = this.root.SelectSingleNode("/x:library_visual_scenes", this.nsManager);
        if (this.visual_scenes == null)
        {
            this.visual_scenes = this.root.AppendChild(this.xml.CreateElement("library_visual_scenes", COLLADA));
            this.default_scene = this.visual_scenes.AppendChild(this.xml.CreateElement("visual_scene", COLLADA));
            attr = this.xml.CreateAttribute("id");
            attr.Value = "Scene";
            this.default_scene.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("name");
            attr.Value = "Scene";
            this.default_scene.Attributes.Append(attr);
        }
        this.scene = this.root.SelectSingleNode("/x:library_scene", this.nsManager);
        if (this.scene == null)
        {
            this.scene = this.root.AppendChild(this.xml.CreateElement("scene", COLLADA));
            node = this.scene.AppendChild(this.xml.CreateElement("instance_visual_scene", COLLADA));
            attr = this.xml.CreateAttribute("url");
            attr.Value = "#Scene";
            node.Attributes.Append(attr);
        }
    }

    public void Dispose()
    {
    }

    public void Save()
    {
        this.xml.Save(this.path);
    }

    private string MakeIdFromName(string name) {
        return name.Replace ('.', '_'); 
    }

    private void SetIdAttribute(XmlNode node, string id) {
        SetAttribute (node, "id", id);
    }

    private void SetAttribute(XmlNode node, string attrName, string attrValue) {
        XmlAttribute attr = this.xml.CreateAttribute(attrName);
        attr.Value = attrValue;
        node.Attributes.Append(attr);
    }

    private void SetNameIdAttributes(XmlNode node, string type, string name, string id, bool setSID = false) {
        SetIdAttribute (node, id);
        if (setSID) {
            SetAttribute (node, "sid", id);
        }
        SetAttribute (node, "type", type);
        SetAttribute (node, "name", name);
    }

    private void SetSidAndText(XmlNode node, string sid, string text) {
        XmlAttribute attr = this.xml.CreateAttribute("sid");
        attr.Value = sid;
        node.Attributes.Append(attr);
        node.AppendChild(this.xml.CreateTextNode(text));
    }

    private string FormatVector(Vector3 v) {
        return string.Format ("{0} {1} {2}", v.x, v.y, v.z);
    }

    private string FormatVector(Vector4 v) {
        return string.Format ("{0} {1} {2} {3}", v.x, v.y, v.z, v.w);
    }

    public XmlNode AddAnimationClip(AnimationClip anim, GameObject targetObject) {

        var rootId = targetObject.name;
        var animID = anim.name;

        XmlNode animationClipNode = this.animation_clips.AppendChild (this.xml.CreateElement ("animation_clip", COLLADA));
        SetIdAttribute(animationClipNode, animID);
        SetAttribute(animationClipNode, "name", anim.name);
        SetAttribute(animationClipNode, "start", "0");
        SetAttribute(animationClipNode, "end", string.Format("{0:F6}", anim.length));

        var bindings = AnimationUtility.GetCurveBindings (anim);

        var paths = bindings.Select (b => b.path).Distinct ().ToList ();

        for (int i = 0; i < paths.Count; ++i) {
            var path = paths [i];
            var transformBindings = bindings.Where (b => b.path == path && b.type == typeof(Transform)).ToArray ();
            var curves = CreateTransformAnimationCurves (anim, transformBindings);

            var curveId = string.Format ("{0}{1}", animID, i);
            var jointId = Path.GetFileName(path);
            if (string.IsNullOrEmpty (jointId)) {
                jointId = rootId;
            }

            AddAnimationCurve(curves, curveId, jointId);

            XmlNode instanceAnimation = animationClipNode.AppendChild (this.xml.CreateElement ("instance_animation", COLLADA));
            SetAttribute(instanceAnimation, "url", string.Format ("#{0}", curveId));
        }

        return animationClipNode;
    }

    private TransformAnimationCurves CreateTransformAnimationCurves(AnimationClip anim, EditorCurveBinding[] bindings) {
        var curves = new TransformAnimationCurves ();

        var positionX = bindings.Where (b => b.propertyName == "m_LocalPosition.x").FirstOrDefault();
        var positionY = bindings.Where (b => b.propertyName == "m_LocalPosition.y").FirstOrDefault();
        var positionZ = bindings.Where (b => b.propertyName == "m_LocalPosition.z").FirstOrDefault();

        var rotationX = bindings.Where (b => b.propertyName == "m_LocalRotation.x").FirstOrDefault();
        var rotationY = bindings.Where (b => b.propertyName == "m_LocalRotation.y").FirstOrDefault();
        var rotationZ = bindings.Where (b => b.propertyName == "m_LocalRotation.z").FirstOrDefault();
        var rotationW = bindings.Where (b => b.propertyName == "m_LocalRotation.w").FirstOrDefault();

        var scaleX = bindings.Where (b => b.propertyName == "m_LocalScale.x").FirstOrDefault();
        var scaleY = bindings.Where (b => b.propertyName == "m_LocalScale.y").FirstOrDefault();
        var scaleZ = bindings.Where (b => b.propertyName == "m_LocalScale.z").FirstOrDefault();

        curves.positionX = AnimationUtility.GetEditorCurve (anim, positionX);
        curves.positionY = AnimationUtility.GetEditorCurve (anim, positionY);
        curves.positionZ = AnimationUtility.GetEditorCurve (anim, positionZ);
        curves.rotationX = AnimationUtility.GetEditorCurve (anim, rotationX);
        curves.rotationY = AnimationUtility.GetEditorCurve (anim, rotationY);
        curves.rotationZ = AnimationUtility.GetEditorCurve (anim, rotationZ);
        curves.rotationW = AnimationUtility.GetEditorCurve (anim, rotationW);
        curves.scaleX = AnimationUtility.GetEditorCurve (anim, scaleX);
        curves.scaleY = AnimationUtility.GetEditorCurve (anim, scaleY);
        curves.scaleZ = AnimationUtility.GetEditorCurve (anim, scaleZ);

        return curves;
    }

    private XmlNode AddAnimationCurve(TransformAnimationCurves curves, string curveId, string jointId) {

        XmlNode curveNode = this.animations.AppendChild (this.xml.CreateElement ("animation", COLLADA));
        SetIdAttribute(curveNode, curveId);

        int keyCount = curves.positionX.length;

        // input
        string inputID = string.Format ("{0}-input", curveId);
        {
            XmlNode inputNode = curveNode.AppendChild (this.xml.CreateElement ("source", COLLADA));
            SetIdAttribute(inputNode, inputID);

            StringBuilder sb = new StringBuilder ();

            foreach (var k in curves.positionX.keys) {
                sb.AppendFormat ("{0:F6} ", k.time);
            }

            string arrayId = string.Format ("{0}-array", inputID);
            XmlNode flArrayNode = inputNode.AppendChild (this.xml.CreateElement ("float_array", COLLADA));
            SetIdAttribute(flArrayNode, arrayId);
            SetAttribute(flArrayNode, "count", keyCount.ToString());
            flArrayNode.AppendChild(this.xml.CreateTextNode(sb.ToString()));

            AddAnimationCurveTechniqueNode (inputNode, keyCount, 1, arrayId, "TIME", "float");
        }

        //output
        string outputID = string.Format ("{0}-output", curveId);
        {
            XmlNode outputNode = curveNode.AppendChild (this.xml.CreateElement ("source", COLLADA));
            SetIdAttribute(outputNode, outputID);

            StringBuilder sb = new StringBuilder ();

            for (int i = 0; i < curves.positionX.length; ++i) {
                var mat = curves.CreateTransformMatrix (i); // already in right-handed coordinate
                var matStr = FormtMatrix4x4ForCollada (ref mat);
                sb.Append (matStr);
            }

            string arrayId = string.Format ("{0}-array", outputID);
            XmlNode flArrayNode = outputNode.AppendChild (this.xml.CreateElement ("float_array", COLLADA));
            SetIdAttribute(flArrayNode, arrayId);
            SetAttribute(flArrayNode, "count", (keyCount * 16).ToString());
            flArrayNode.AppendChild(this.xml.CreateTextNode(sb.ToString()));

            AddAnimationCurveTechniqueNode (outputNode, keyCount, 16, arrayId, "TRANSFORM", "float4x4");
        }

        //interpolation
        string interpolationID = string.Format ("{0}-interpolation", curveId);
        {
            XmlNode interpolationNode = curveNode.AppendChild (this.xml.CreateElement ("source", COLLADA));
            SetIdAttribute(interpolationNode, interpolationID);

            StringBuilder sb = new StringBuilder ();

            foreach (var k in curves.positionX.keys) {
                sb.Append ("LINEAR ");
            }

            string arrayId = string.Format ("{0}-array", outputID);
            XmlNode flArrayNode = interpolationNode.AppendChild (this.xml.CreateElement ("Name_array", COLLADA));
            SetIdAttribute(flArrayNode, arrayId);
            SetAttribute(flArrayNode, "count", keyCount.ToString());
            flArrayNode.AppendChild(this.xml.CreateTextNode(sb.ToString()));

            AddAnimationCurveTechniqueNode (interpolationNode, keyCount, 1, arrayId, "INTERPOLATION", "name");
        }

        //sampler
        string samplerID = string.Format ("{0}-sampler", curveId);
        {
            var samplerNode = curveNode.AppendChild (this.xml.CreateElement ("sampler", COLLADA));
            SetIdAttribute(samplerNode, samplerID);

            var sin = samplerNode.AppendChild (this.xml.CreateElement ("input", COLLADA));
            SetAttribute(sin, "semantic", "INPUT");
            SetAttribute(sin, "source", string.Format("#{0}", inputID));

            var sout = samplerNode.AppendChild (this.xml.CreateElement ("input", COLLADA));
            SetAttribute(sout, "semantic", "OUTPUT");
            SetAttribute(sout, "source", string.Format("#{0}", outputID));

            var sinterpol = samplerNode.AppendChild (this.xml.CreateElement ("input", COLLADA));
            SetAttribute(sinterpol, "semantic", "INTERPOLATION");
            SetAttribute(sinterpol, "source", string.Format("#{0}", interpolationID));
        }

        //channel
        {
            var channelNode = curveNode.AppendChild (this.xml.CreateElement ("channel", COLLADA));
            SetAttribute(channelNode, "source", string.Format("#{0}", samplerID));
            SetAttribute(channelNode, "target", string.Format("{0}/matrix", jointId));
        }

        return curveNode;
    }

    private void AddAnimationCurveTechniqueNode(XmlNode node, int count, int stride, string sourceId, string paramName, string paramType) {
        XmlNode techniqueCommonNode = node.AppendChild (this.xml.CreateElement ("technique_common", COLLADA));
        XmlNode accessorNode = techniqueCommonNode.AppendChild (this.xml.CreateElement ("accessor", COLLADA));
        XmlNode paramNode = accessorNode.AppendChild (this.xml.CreateElement ("param", COLLADA));

        SetAttribute(accessorNode, "source", string.Format("#{0}", sourceId));
        SetAttribute(accessorNode, "count", count.ToString());
        if (stride > 1) {
            SetAttribute(accessorNode, "stride", stride.ToString());
        }

        SetAttribute(paramNode, "name", paramName);
        SetAttribute(paramNode, "type", paramType);
    }

    public XmlNode AddObjectToScene(GameObject sourceObject, bool addMesh = true)
    {
        XmlNode node = this.default_scene.AppendChild (this.xml.CreateElement ("node", COLLADA));
        SetNameIdAttributes (node, "NODE", sourceObject.name, MakeIdFromName (sourceObject.name), false);

        XmlNode location = node.AppendChild (this.xml.CreateElement ("translate", COLLADA));
        XmlNode rotationX = node.AppendChild (this.xml.CreateElement ("rotate", COLLADA));
        XmlNode rotationY = node.AppendChild (this.xml.CreateElement ("rotate", COLLADA));
        XmlNode rotationZ = node.AppendChild (this.xml.CreateElement ("rotate", COLLADA));
        XmlNode scale   = node.AppendChild (this.xml.CreateElement ("scale", COLLADA));

        Transform t = sourceObject.transform;

        Matrix4x4 rot = new Matrix4x4 ();
        var r = t.localRotation;
        r = new Quaternion (-r.x, r.y, r.z, -r.w);

        rot.SetTRS (Vector3.zero, r, Vector3.one);

        SetSidAndText(location, "location", FormatVector(t.position));
        SetSidAndText(rotationX, "rotationX", FormatVector(rot.GetRow(0)));
        SetSidAndText(rotationY, "rotationY", FormatVector(rot.GetRow(1)));
        SetSidAndText(rotationZ, "rotationZ", FormatVector(rot.GetRow(2)));
        SetSidAndText(scale, "scale", FormatVector(t.localScale));

        for (int i = 0; i < t.childCount; ++i) {
            Transform child = t.GetChild (i);

            var components = child.GetComponents<Component> ();

            if (components.Length <= 1) {
                AddJointToScene (child, node, addMesh);
            } else {
                AddNodeToScene (child, node, addMesh);
            }
        }
        return node;
    }

    private void AddNodeToScene(Transform t, XmlNode parent, bool addMesh)
    {
        XmlNode node = parent.AppendChild (this.xml.CreateElement ("node", COLLADA));
        SetNameIdAttributes (node, "NODE", t.name, MakeIdFromName (t.name), true);

        XmlNode matrix = node.AppendChild (this.xml.CreateElement ("matrix", COLLADA));
        SetSidAndText(matrix, "matrix", CreateMatrixStringFromTRSForCollada(t.localPosition, t.localRotation, t.localScale));

        if (addMesh) {
            AddMeshIfAny (t, node);
        }
    }

    private void AddMeshIfAny(Transform t, XmlNode node) {
        
        var meshFilter = t.gameObject.GetComponent<MeshFilter> ();
        var skinnedmesh = t.gameObject.GetComponent<SkinnedMeshRenderer> ();

        Mesh mesh = null;
        if (meshFilter != null) {
            mesh = meshFilter.sharedMesh;
        }

        if (skinnedmesh != null) {
            mesh = skinnedmesh.sharedMesh;
        }

        if (mesh != null) {
            var geometryId = MakeIdFromName(mesh.name);
            AddGeometry(geometryId, mesh.name, mesh);
            AddGeometryInstanceToScene (geometryId, node);
        }
    }

    private void AddJointToScene(Transform t, XmlNode parent, bool addMesh)
    {
        XmlNode node = parent.AppendChild (this.xml.CreateElement ("node", COLLADA));
        SetNameIdAttributes (node, "JOINT", t.name, MakeIdFromName (t.name), true);

        XmlNode matrix = node.AppendChild (this.xml.CreateElement ("matrix", COLLADA));

        string text = CreateMatrixStringFromTRSForCollada (t.localPosition, t.localRotation, t.localScale);

        SetSidAndText(matrix, "matrix", text);

        if (addMesh) {
            AddMeshIfAny (t, node);
        }

        for (int i = 0; i < t.childCount; ++i) {
            Transform child = t.GetChild (i);
            AddJointToScene (child, node, addMesh);
        }
    }

    private string CreateMatrixStringFromTRSForCollada(Vector3 pos, Quaternion rot, Vector3 scale) {
        Matrix4x4 mat = new Matrix4x4 ();
        //convert right-hand system/left-hand system (to-from Collada(right) <-> Unity(left))
        pos = new Vector3(-pos.x, pos.y, pos.z);
        rot = new Quaternion (-rot.x, rot.y, rot.z, -rot.w);

        mat.SetTRS (pos, rot, scale);

        return FormtMatrix4x4ForCollada (ref mat);
    }

    private static string FormtMatrix4x4ForCollada(ref Matrix4x4 mat) {
        return string.Format (
            "\n{0:F6}\t{1:F6}\t{2:F6}\t{3:F6}\n{4:F6}\t{5:F6}\t{6:F6}\t{7:F6}\n{8:F6}\t{9:F6}\t{10:F6}\t{11:F6}\n{12:F6}\t{13:F6}\t{14:F6}\t{15:F6}\n",
            mat.m00, mat.m01, mat.m02, mat.m03,
            mat.m10, mat.m11, mat.m12, mat.m13,
            mat.m20, mat.m21, mat.m22, mat.m23,
            mat.m30, mat.m31, mat.m32, mat.m33
            );
    }

    public XmlNode AddGeometry(Mesh sourceMesh) {
        return AddGeometry(MakeIdFromName(sourceMesh.name), sourceMesh.name, sourceMesh);
    }

    private XmlNode AddGeometry(string id, string name, Mesh sourceMesh)
    {
        XmlNode geometry = this.geometries.AppendChild(this.xml.CreateElement("geometry", COLLADA));
        XmlNode mesh = geometry.AppendChild(this.xml.CreateElement("mesh", COLLADA));
        XmlNode nodeA, nodeB, nodeC, nodeD;
        XmlAttribute attr;
        StringBuilder str;

        attr = this.xml.CreateAttribute("id");
        attr.Value = id + "-mesh";
        geometry.Attributes.Append(attr);

        attr = this.xml.CreateAttribute("name");
        attr.Value = name;
        geometry.Attributes.Append(attr);

        // Positions
        if (sourceMesh.vertexCount > 0 )
        {
            nodeA = mesh.AppendChild(this.xml.CreateElement("source", COLLADA));
            attr = this.xml.CreateAttribute("id");
            attr.Value = id + "-mesh-positions";
            nodeA.Attributes.Append(attr);

            nodeB = nodeA.AppendChild(this.xml.CreateElement("float_array", COLLADA));
            attr = this.xml.CreateAttribute("id");
            attr.Value = id + "-mesh-positions-array";
            nodeB.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("count");
            attr.Value = (sourceMesh.vertexCount * 3).ToString();
            nodeB.Attributes.Append(attr);

            str = new StringBuilder();
            for (int i = 0, n = sourceMesh.vertexCount; i < n; ++i)
            {
                str.Append((-sourceMesh.vertices[i].x).ToString());
                str.Append(" ");
                str.Append(sourceMesh.vertices[i].y.ToString());
                str.Append(" ");
                str.Append(sourceMesh.vertices[i].z.ToString());
                if (i + 1 != n)
                    str.Append(" ");
            }
            nodeB.AppendChild(this.xml.CreateTextNode(str.ToString()));
            str = null;

            nodeB = nodeA.AppendChild(this.xml.CreateElement("technique_common", COLLADA));
            nodeC = nodeB.AppendChild(this.xml.CreateElement("accessor", COLLADA));
            attr = this.xml.CreateAttribute("source");
            attr.Value = "#" + id + "-mesh-positions-array";
            nodeC.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("count");
            attr.Value = sourceMesh.vertexCount.ToString();
            nodeC.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("stride");
            attr.Value = "3";
            nodeC.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "X";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "Y";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "Z";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
        }

        // Colors
        if (sourceMesh.colors.Length > 0)
        {
            nodeA = mesh.AppendChild(this.xml.CreateElement("source", COLLADA));
            attr = this.xml.CreateAttribute("id");
            attr.Value = id + "-mesh-colors";
            nodeA.Attributes.Append(attr);

            nodeB = nodeA.AppendChild(this.xml.CreateElement("float_array", COLLADA));
            attr = this.xml.CreateAttribute("id");
            attr.Value = id + "-mesh-colors-array";
            nodeB.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("count");
            attr.Value = (sourceMesh.colors.Length * 3).ToString();
            nodeB.Attributes.Append(attr);

            str = new StringBuilder();
            for (int i = 0, n = sourceMesh.colors.Length; i < n; ++i)
            {
                //str.Append(mesh.colors[i].a.ToString());
                //str.Append(" ");
                str.Append(sourceMesh.colors[i].r.ToString());
                str.Append(" ");
                str.Append(sourceMesh.colors[i].g.ToString());
                str.Append(" ");
                str.Append(sourceMesh.colors[i].b.ToString());
                if (i + 1 != n)
                    str.Append(" ");
            }
            nodeB.AppendChild(this.xml.CreateTextNode(str.ToString()));
            str = null;

            nodeB = nodeA.AppendChild(this.xml.CreateElement("technique_common", COLLADA));
            nodeC = nodeB.AppendChild(this.xml.CreateElement("accessor", COLLADA));
            attr = this.xml.CreateAttribute("source");
            attr.Value = "#" + id + "-mesh-colors-array";
            nodeC.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("count");
            attr.Value = sourceMesh.colors.Length.ToString();
            nodeC.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("stride");
            attr.Value = "3";
            nodeC.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "R";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "G";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "B";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
        }

        // Normals
        if (sourceMesh.normals.Length > 0)
        {
            nodeA = mesh.AppendChild(this.xml.CreateElement("source", COLLADA));
            attr = this.xml.CreateAttribute("id");
            attr.Value = id + "-mesh-normals";
            nodeA.Attributes.Append(attr);

            nodeB = nodeA.AppendChild(this.xml.CreateElement("float_array", COLLADA));
            attr = this.xml.CreateAttribute("id");
            attr.Value = id + "-mesh-normals-array";
            nodeB.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("count");
            attr.Value = (sourceMesh.normals.Length * 3).ToString();
            nodeB.Attributes.Append(attr);

            str = new StringBuilder();
            for (int i = 0, n = sourceMesh.normals.Length; i < n; ++i)
            {
                str.Append((-sourceMesh.normals[i].x).ToString());
                str.Append(" ");
                str.Append(sourceMesh.normals[i].y.ToString());
                str.Append(" ");
                str.Append(sourceMesh.normals[i].z.ToString());
                if (i + 1 != n)
                    str.Append(" ");
            }
            nodeB.AppendChild(this.xml.CreateTextNode(str.ToString()));
            str = null;

            nodeB = nodeA.AppendChild(this.xml.CreateElement("technique_common", COLLADA));
            nodeC = nodeB.AppendChild(this.xml.CreateElement("accessor", COLLADA));
            attr = this.xml.CreateAttribute("source");
            attr.Value = "#" + id + "-mesh-normals-array";
            nodeC.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("count");
            attr.Value = sourceMesh.normals.Length.ToString();
            nodeC.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("stride");
            attr.Value = "3";
            nodeC.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "X";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "Y";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
            nodeD = nodeC.AppendChild(this.xml.CreateElement("param", COLLADA));
            attr = this.xml.CreateAttribute("name");
            attr.Value = "Z";
            nodeD.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("type");
            attr.Value = "float";
            nodeD.Attributes.Append(attr);
        }

        // Vertices
        {
            nodeA = mesh.AppendChild(this.xml.CreateElement("vertices", COLLADA));
            attr = this.xml.CreateAttribute("id");
            attr.Value = id + "-mesh-vertices";
            nodeA.Attributes.Append(attr);

            if (sourceMesh.vertexCount > 0)
            {
                nodeB = nodeA.AppendChild(this.xml.CreateElement("input", COLLADA));
                attr = this.xml.CreateAttribute("semantic");
                attr.Value = "POSITION";
                nodeB.Attributes.Append(attr);
                attr = this.xml.CreateAttribute("source");
                attr.Value = "#" + id + "-mesh-positions";
                nodeB.Attributes.Append(attr);
            }

            if (sourceMesh.normals.Length > 0)
            {
                nodeB = nodeA.AppendChild(this.xml.CreateElement("input", COLLADA));
                attr = this.xml.CreateAttribute("semantic");
                attr.Value = "NORMAL";
                nodeB.Attributes.Append(attr);
                attr = this.xml.CreateAttribute("source");
                attr.Value = "#" + id + "-mesh-normals";
                nodeB.Attributes.Append(attr);
            }

            if (sourceMesh.colors.Length > 0)
            {
                nodeB = nodeA.AppendChild(this.xml.CreateElement("input", COLLADA));
                attr = this.xml.CreateAttribute("semantic");
                attr.Value = "COLOR";
                nodeB.Attributes.Append(attr);
                attr = this.xml.CreateAttribute("source");
                attr.Value = "#" + id + "-mesh-colors";
                nodeB.Attributes.Append(attr);
            }
        }

        // Triangles
        {
            nodeA = mesh.AppendChild(this.xml.CreateElement("triangles", COLLADA));
            attr = this.xml.CreateAttribute("count");
            attr.Value = (sourceMesh.triangles.Length / 3).ToString();
            nodeA.Attributes.Append(attr);

            nodeB = nodeA.AppendChild(this.xml.CreateElement("input", COLLADA));
            attr = this.xml.CreateAttribute("semantic");
            attr.Value = "VERTEX";
            nodeB.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("source");
            attr.Value = "#" + id + "-mesh-vertices";
            nodeB.Attributes.Append(attr);
            attr = this.xml.CreateAttribute("offset");
            attr.Value = "0";
            nodeB.Attributes.Append(attr);

            nodeB = nodeA.AppendChild(this.xml.CreateElement("p", COLLADA));

            str = new StringBuilder();
            //for (int i = 0, n = mesh.triangles.Length; i < n; ++i)
            for (int i = sourceMesh.triangles.Length - 1; i >= 0; --i)
            {
                str.Append(sourceMesh.triangles[i].ToString());
                if (i != 0)
                    str.Append(" ");
            }

            nodeB.AppendChild(this.xml.CreateTextNode(str.ToString()));
            str = null;
        }

        return geometry;
    }

    private XmlNode AddGeometryInstanceToScene(string id, XmlNode parent)
    {
        XmlNode node;
        XmlAttribute attr;

        node = parent.AppendChild(this.xml.CreateElement("instance_geometry", COLLADA));
        attr = this.xml.CreateAttribute("url");
        attr.Value = "#" + id + "-mesh";
        node.Attributes.Append(attr);

        return node;
    }
}