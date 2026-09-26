using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// A built humanoid body's head and neck, read back in a frame of their own - x to the body's
    /// right, y up, z the way it faces, the origin at the head bone - for the pieces fitted to them:
    /// grown hair, glasses, earrings, a cap, a necklace.
    ///
    /// <para>Read from the posed mesh itself rather than assumed, so a piece fits every face and
    /// every proportion slider, on both bodies. The skin is told from what sits on it - hair cards,
    /// eyes, clothes - by material: it is the part drawn with the skin shader.</para>
    /// </summary>
    internal sealed class HeadScan
    {
        public Transform Root, Head, NeckBone, ChestBone;
        public Vector3 Origin, Right, Up, Forward;
        /// <summary>The body's own axes when the scan was taken, which the head's may be turned or tilted from.</summary>
        public Vector3 BodyRight, BodyUp, BodyForward;
        private HeadScan neckInBodyAxes;

        /// <summary>The head's skin, welded where texture seams split it: points, smooth outward normals, triangles.</summary>
        public Vector3[] Points, Normals;
        public int[] Triangles;
        /// <summary>
        /// How much of each point is the head's own rather than the neck's - the least, where the
        /// skin welded at a point disagrees. Skin shared with the neck, low at the nape, moves as
        /// the head turns, so it is read where it is only for the head as it was.
        /// </summary>
        public float[] HeadShares;

        /// <summary>The head skin's bounds.</summary>
        public Vector3 Centre, Extent;
        /// <summary>The head's own skin by direction from <see cref="Centre"/>: see <see cref="SkinRadius"/>.</summary>
        private float[,] skinEnvelope;
        private const int EnvelopeRows = 18, EnvelopeColumns = 36;
        /// <summary>The height of the highest point of everything the head carries - skin, hair cards, brows, grown hair already fitted.</summary>
        public float CarriedTopY;

        public float EyeY, CrownY, ChinY;
        public Vector3 LeftEye, RightEye;
        /// <summary>Half the skull's width above the ears; skin wider than this at ear height is ear.</summary>
        public float SkullHalfWidth;
        /// <summary>The foot of each ear, where an earring hangs, and its top, where glasses rest.</summary>
        public Vector3 LeftLobe, RightLobe, LeftEarTop, RightEarTop;

        /// <summary>Everything round the neck and shoulders, any material - skin or the clothes over it - for what drapes there.</summary>
        public Vector3[] Collar;
        public Vector3 Neck, Chest, LeftShoulder, RightShoulder;
        public bool HasTorso;

        public float Height => CrownY - EyeY;
        public Vector3 ToWorld(Vector3 p) => Origin + Right * p.x + Up * p.y + Forward * p.z;
        /// <summary>The frame as a matrix: frame coordinates in, world positions out.</summary>
        public Matrix4x4 FrameToWorld => new Matrix4x4(Right, Up, Forward, new Vector4(Origin.x, Origin.y, Origin.z, 1f));
        public Vector3 ToWorldDirection(Vector3 d) => Right * d.x + Up * d.y + Forward * d.z;

        public Vector3 ToFrame(Vector3 world)
        {
            var d = world - Origin;
            return new Vector3(Vector3.Dot(d, Right), Vector3.Dot(d, Up), Vector3.Dot(d, Forward));
        }

        /// <summary>Degrees round the head from straight ahead, positive to the body's right.</summary>
        public float Around(Vector3 p) => Mathf.Atan2(p.x - Centre.x, p.z - Centre.z) * Mathf.Rad2Deg;

        /// <summary>
        /// The neck and shoulders of this scan in the body's own axes rather than the head's, for
        /// what hangs from the neck or the chest: a necklace, a bow tie, the lower part of long
        /// strands. Built in the head's axes, a bow tie scanned while the head was turned stayed
        /// turned on the collar for good, and a necklace's lowest point hung off to one side.
        ///
        /// <para>Only the neck is carried over - the collar, the neck, the chest, the shoulders and
        /// the chin line - with the same origin; the head's own points are left behind, since
        /// nothing built in this frame reads them.</para>
        /// </summary>
        public HeadScan NeckInBodyAxes()
        {
            if (neckInBodyAxes != null) return neckInBodyAxes;
            var body = new HeadScan
            {
                Root = Root, Head = Head, NeckBone = NeckBone, ChestBone = ChestBone, HasTorso = HasTorso,
                Origin = Origin, Right = BodyRight, Up = BodyUp, Forward = BodyForward,
                BodyRight = BodyRight, BodyUp = BodyUp, BodyForward = BodyForward,
            };
            body.Collar = new Vector3[Collar != null ? Collar.Length : 0];
            for (int i = 0; i < body.Collar.Length; i++) body.Collar[i] = body.ToFrame(ToWorld(Collar[i]));
            body.Neck = body.ToFrame(ToWorld(Neck));
            body.Chest = body.ToFrame(ToWorld(Chest));
            body.LeftShoulder = body.ToFrame(ToWorld(LeftShoulder));
            body.RightShoulder = body.ToFrame(ToWorld(RightShoulder));
            body.ChinY = body.ToFrame(ToWorld(new Vector3(Centre.x, ChinY, Centre.z))).y;
            body.neckInBodyAxes = body;
            neckInBodyAxes = body;
            return body;
        }

        /// <summary>
        /// The same head, in the same frame coordinates, set upright on the body: as if it faced the
        /// way the body does rather than wherever it was turned when the scan was taken. What is
        /// built in it is built for a head facing forward, falling as it would fall from there;
        /// <see cref="FrameToWorld"/> of this scan times the inverse of the straightened one's
        /// carries it onto the head as it is now. The scan's frame follows the head's turn and tilt,
        /// by the eye line, but takes its up from the body, so a nod stays in the head's
        /// coordinates: a houseguest's fits are made with the body standing (UmaBodyTint), nods
        /// and all taken off.
        /// The neck and shoulders do not turn with the head, so they are the body's own, as they are.
        /// </summary>
        public HeadScan Straightened()
        {
            var straight = (HeadScan)MemberwiseClone();
            straight.measured = measured ?? new[] { Right, Up, Forward };
            straight.Right = BodyRight; straight.Up = BodyUp; straight.Forward = BodyForward;
            straight.neckInBodyAxes = NeckInBodyAxes();
            return straight;
        }

        // The axes the head's points were read along, which a straightened scan keeps; null while
        // they are the frame's own.
        private Vector3[] measured;

        /// <summary>A direction in the world, in the coordinates the head's points are measured in: the frame's own, or the scan's it was straightened from.</summary>
        public Vector3 ToMeasuredDirection(Vector3 world)
        {
            Vector3 right = measured != null ? measured[0] : Right, up = measured != null ? measured[1] : Up, forward = measured != null ? measured[2] : Forward;
            return new Vector3(Vector3.Dot(world, right), Vector3.Dot(world, up), Vector3.Dot(world, forward));
        }

        /// <summary>How far out from <see cref="Centre"/> the head's own skin reaches in a direction, with nothing it carries.</summary>
        public float SkinRadius(Vector3 direction) => Sample(skinEnvelope, direction);

        /// <summary>How much the head carries above its crown - its hair, standing up off the scalp - and nothing for a bare head.</summary>
        public float HairOnTop => Mathf.Max(0f, CarriedTopY - CrownY);

        private static float Sample(float[,] envelope, Vector3 direction)
        {
            direction.Normalize();
            float row = Mathf.Acos(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI * EnvelopeRows;
            float column = (Mathf.Atan2(direction.x, direction.z) / (Mathf.PI * 2f) + .5f) * EnvelopeColumns;
            int r0 = Mathf.Clamp(Mathf.FloorToInt(row - .5f), 0, EnvelopeRows - 1), r1 = Mathf.Min(r0 + 1, EnvelopeRows - 1);
            int c0 = ((Mathf.FloorToInt(column - .5f) % EnvelopeColumns) + EnvelopeColumns) % EnvelopeColumns, c1 = (c0 + 1) % EnvelopeColumns;
            float fr = Mathf.Clamp01(row - .5f - r0), fc = column - .5f - Mathf.Floor(column - .5f);
            return Mathf.Lerp(Mathf.Lerp(envelope[r0, c0], envelope[r0, c1], fc), Mathf.Lerp(envelope[r1, c0], envelope[r1, c1], fc), fr);
        }

        /// <summary>
        /// The skin's points that <paramref name="keep"/> accepts, as a welded mesh of their own:
        /// the triangles whose three corners are all kept.
        /// </summary>
        public void Region(Func<Vector3, bool> keep, out Vector3[] points, out Vector3[] normals, out int[] triangles)
            => Region(keep, out points, out normals, out triangles, out _);

        /// <summary>As <see cref="Region(Func{Vector3, bool}, out Vector3[], out Vector3[], out int[])"/>, with each kept point's <see cref="HeadShares"/>.</summary>
        public void Region(Func<Vector3, bool> keep, out Vector3[] points, out Vector3[] normals, out int[] triangles, out float[] shares)
        {
            var remap = new int[Points.Length];
            var kept = new List<Vector3>();
            var keptNormals = new List<Vector3>();
            var keptShares = new List<float>();
            for (int i = 0; i < Points.Length; i++)
            {
                remap[i] = -1;
                if (!keep(Points[i])) continue;
                remap[i] = kept.Count;
                kept.Add(Points[i]);
                keptNormals.Add(Normals[i]);
                keptShares.Add(HeadShares != null && i < HeadShares.Length ? HeadShares[i] : 1f);
            }
            shares = keptShares.ToArray();
            var tris = new List<int>();
            for (int t = 0; t + 2 < Triangles.Length; t += 3)
            {
                int a = remap[Triangles[t]], b = remap[Triangles[t + 1]], c = remap[Triangles[t + 2]];
                if (a < 0 || b < 0 || c < 0) continue;
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            points = kept.ToArray();
            normals = keptNormals.ToArray();
            triangles = tris.ToArray();
        }

        /// <summary>The farthest the skin reaches from <paramref name="axis"/>, horizontally, near a height and a bearing.</summary>
        public float SkinReach(Vector3 axis, float y, float degrees, float heightWindow = .012f, float bearingWindow = 6f)
        {
            float best = 0f;
            foreach (var p in Points)
            {
                if (Mathf.Abs(p.y - y) > heightWindow) continue;
                var d = new Vector2(p.x - axis.x, p.z - axis.z);
                if (Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, degrees)) > bearingWindow) continue;
                best = Mathf.Max(best, d.magnitude);
            }
            return best;
        }

        /// <summary>The farthest the collar - skin or clothes - reaches from <paramref name="axis"/> near a height and a bearing.</summary>
        public float CollarReach(Vector3 axis, float y, float degrees, float heightWindow = .012f, float bearingWindow = 7f)
        {
            float best = 0f;
            if (Collar == null) return best;
            foreach (var p in Collar)
            {
                if (Mathf.Abs(p.y - y) > heightWindow) continue;
                var d = new Vector2(p.x - axis.x, p.z - axis.z);
                if (Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, degrees)) > bearingWindow) continue;
                best = Mathf.Max(best, d.magnitude);
            }
            return best;
        }

        /// <summary>Reads a built body's head. Null when it has no humanoid head, or no readable skinned mesh on it.</summary>
        public static HeadScan Read(GameObject body)
        {
            if (body == null) return null;
            var animator = body.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return null;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) return null;
            SkinnedMeshRenderer renderer = null;
            foreach (var candidate in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var candidateMesh = candidate.sharedMesh;
                // Long grown strands are skinned to the head too; they are not the head.
                if (candidate.GetComponent<GrownPiece>() != null) continue;
                if (candidateMesh == null || !candidateMesh.isReadable || Array.IndexOf(candidate.bones, head) < 0) continue;
                if (renderer == null || candidateMesh.vertexCount > renderer.sharedMesh.vertexCount) renderer = candidate;
            }
            if (renderer == null) return null;

            var root = animator.transform;
            var scan = new HeadScan
            {
                Root = root, Head = head, Origin = head.position, Right = root.right, Up = root.up, Forward = root.forward,
                BodyRight = root.right, BodyUp = root.up, BodyForward = root.forward,
                NeckBone = animator.GetBoneTransform(HumanBodyBones.Neck),
                ChestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                            ?? animator.GetBoneTransform(HumanBodyBones.Spine),
            };
            HeadAxes(scan, animator);
            var bones = renderer.bones;
            var headBone = new bool[bones.Length];
            var neckBone = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                headBone[i] = bones[i] != null && (bones[i] == head || bones[i].IsChildOf(head));
                neckBone[i] = bones[i] != null && scan.NeckBone != null && bones[i] == scan.NeckBone;
            }
            var weights = renderer.sharedMesh.boneWeights;
            var baked = new Mesh();
            renderer.BakeMesh(baked, true);
            var raw = baked.vertices;
            if (weights.Length != raw.Length) { GrownPiece.Dispose(baked); return null; }
            var toWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);

            var frame = new Vector3[raw.Length];
            var onHead = new bool[raw.Length];
            var shares = new float[raw.Length];
            var collar = new List<Vector3>();
            var neckWorld = scan.NeckBone != null ? scan.NeckBone.position : head.position;
            var neckFrame = scan.ToFrame(neckWorld);
            for (int i = 0; i < raw.Length; i++)
            {
                var w = weights[i];
                float share = Share(headBone, w);
                shares[i] = share;
                frame[i] = scan.ToFrame(toWorld.MultiplyPoint3x4(raw[i]));
                onHead[i] = share >= .6f;
                // The collar: whatever is near the neck and the top of the chest, in any material.
                var p = frame[i];
                if (!onHead[i] && p.y < neckFrame.y + .03f && p.y > neckFrame.y - .2f && Mathf.Abs(p.x - neckFrame.x) < .22f && Mathf.Abs(p.z - neckFrame.z) < .2f)
                    collar.Add(p);
            }
            scan.Collar = collar.ToArray();

            // The skin is every submesh drawn with the skin shader - UMA splits it across several -
            // and failing any, the one submesh holding most of the head.
            var skinTriangles = new List<int>();
            var materials = renderer.sharedMaterials;
            for (int s = 0; s < baked.subMeshCount && s < materials.Length; s++)
                if (materials[s] != null && materials[s].HasProperty("_Smoothness_Remap")) skinTriangles.AddRange(baked.GetTriangles(s));
            if (skinTriangles.Count == 0)
            {
                int most = -1, skin = -1;
                for (int s = 0; s < baked.subMeshCount; s++)
                {
                    int count = 0;
                    foreach (int index in baked.GetTriangles(s)) if (onHead[index]) count++;
                    if (count > most) { most = count; skin = s; }
                }
                if (skin >= 0) skinTriangles.AddRange(baked.GetTriangles(skin));
            }
            if (skinTriangles.Count == 0) { GrownPiece.Dispose(baked); return null; }

            // Everything on the head, for how high it carries: every submesh, not only the skin, and
            // the grown hair the body already wears.
            var envelopePoints = new List<Vector3>();
            for (int i = 0; i < raw.Length; i++) if (onHead[i]) envelopePoints.Add(frame[i]);
            AddGrownHair(body, scan, envelopePoints);

            var triangles = skinTriangles;
            GrownPiece.Dispose(baked);
            var welded = new Dictionary<Vector3Int, int>();
            var remap = new Dictionary<int, int>();
            var points = new List<Vector3>();
            var pointShares = new List<float>();
            int Weld(int index)
            {
                if (remap.TryGetValue(index, out int found)) return found;
                var key = Vector3Int.RoundToInt(frame[index] * 2000f);
                if (!welded.TryGetValue(key, out found))
                {
                    found = points.Count; points.Add(frame[index]); pointShares.Add(shares[index]); welded.Add(key, found);
                }
                else pointShares[found] = Mathf.Min(pointShares[found], shares[index]);
                remap.Add(index, found);
                return found;
            }
            var tris = new List<int>();
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                int ia = triangles[t], ib = triangles[t + 1], ic = triangles[t + 2];
                if (!onHead[ia] || !onHead[ib] || !onHead[ic]) continue;
                int a = Weld(ia), b = Weld(ib), c = Weld(ic);
                if (a == b || b == c || a == c) continue;
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            if (tris.Count < 90) return null;
            scan.Points = points.ToArray();
            scan.HeadShares = pointShares.ToArray();
            scan.Triangles = tris.ToArray();

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in scan.Points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            scan.Centre = (min + max) * .5f;
            scan.Extent = (max - min) * .5f;
            scan.CrownY = max.y;
            scan.ChinY = min.y;
            scan.Normals = SmoothNormals(scan.Points, scan.Triangles);
            for (int i = 0; i < scan.Normals.Length; i++)
                if (Vector3.Dot(scan.Normals[i], scan.Points[i] - scan.Centre) < 0f) scan.Normals[i] = -scan.Normals[i];

            var left = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var right = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (left != null && right != null) { scan.LeftEye = scan.ToFrame(left.position); scan.RightEye = scan.ToFrame(right.position); }
            else
            {
                float y = Mathf.Lerp(min.y, max.y, .55f);
                scan.LeftEye = new Vector3(scan.Centre.x - scan.Extent.x * .38f, y, max.z - .025f);
                scan.RightEye = new Vector3(scan.Centre.x + scan.Extent.x * .38f, y, max.z - .025f);
            }
            // The humanoid's left is the frame's negative x; keep the names meaning the body's own sides.
            if (scan.LeftEye.x > scan.RightEye.x) { var swap = scan.LeftEye; scan.LeftEye = scan.RightEye; scan.RightEye = swap; }
            scan.EyeY = (scan.LeftEye.y + scan.RightEye.y) * .5f;
            if (scan.CrownY - scan.EyeY < .04f) return null;
            float h = scan.Height;

            float skull = 0f;
            foreach (var p in scan.Points)
                if (p.y > scan.EyeY + .22f * h && p.y < scan.EyeY + .45f * h) skull = Mathf.Max(skull, Mathf.Abs(p.x - scan.Centre.x));
            scan.SkullHalfWidth = skull > 0f ? skull : scan.Extent.x * .8f;

            // The ears, found where anatomy puts them and snapped to the skin: the lobe near the
            // height of the nose's base, the top near the brow, both a little behind the head's middle.
            float earZ = scan.Centre.z - scan.Extent.z * .08f;
            scan.LeftLobe = SnapToSide(scan, -1, scan.EyeY - .36f * h, earZ);
            scan.RightLobe = SnapToSide(scan, 1, scan.EyeY - .36f * h, earZ);
            scan.LeftEarTop = SnapToSide(scan, -1, scan.EyeY + .14f * h, earZ - .004f);
            scan.RightEarTop = SnapToSide(scan, 1, scan.EyeY + .14f * h, earZ - .004f);

            scan.CarriedTopY = scan.CrownY;
            foreach (var p in envelopePoints) scan.CarriedTopY = Mathf.Max(scan.CarriedTopY, p.y);
            scan.skinEnvelope = Envelope(scan, scan.Points);

            var chest = scan.ChestBone;
            var leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            scan.HasTorso = scan.NeckBone != null && chest != null && leftArm != null && rightArm != null;
            if (scan.HasTorso)
            {
                scan.Neck = scan.ToFrame(scan.NeckBone.position);
                scan.Chest = scan.ToFrame(chest.position);
                scan.LeftShoulder = scan.ToFrame(leftArm.position);
                scan.RightShoulder = scan.ToFrame(rightArm.position);
            }
            return scan;
        }

        /// <summary>
        /// The frame's axes, turned as the head is turned: right along the line through the eyes,
        /// whose bones ride on the head, and up the body's own up straightened against it. A scan
        /// taken mid-idle, the head a few degrees round, would otherwise read one ear further
        /// forward than the other and hang one earring out of true. What hangs from the neck or
        /// the chest is built in the body's own axes instead (see <see cref="NeckInBodyAxes"/>).
        /// </summary>
        private static void HeadAxes(HeadScan scan, Animator animator)
        {
            var left = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var right = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (left == null || right == null) return;
            var across = right.position - left.position;
            if (across.sqrMagnitude < 1e-6f) return;
            // The humanoid's right eye is on the body's right; a rig that has them the other way round keeps the body's axes.
            if (Vector3.Dot(across, scan.Right) <= 0f) return;
            across.Normalize();
            var up = Vector3.ProjectOnPlane(scan.Up, across);
            if (up.sqrMagnitude < 1e-6f) return;
            scan.Right = across;
            scan.Up = up.normalized;
            scan.Forward = Vector3.Cross(scan.Right, scan.Up);
        }

        /// <summary>
        /// Adds the grown hair a body already wears to what its head carries, so a head with grown
        /// hair on it is known to carry hair (<see cref="HairOnTop"/>): the hair is a mesh of its own
        /// rather than part of the skinned body. Only hair still worn counts: hair being replaced is
        /// switched off before it goes. Of long strands, only what turns wholly with the head counts,
        /// as only the skin wholly the head's does; below the chin they hang towards the neck. Told
        /// by the piece's own switch rather than the hierarchy's, so a body fitted while it is
        /// switched off still counts its hair.
        /// </summary>
        private static void AddGrownHair(GameObject body, HeadScan scan, List<Vector3> points)
        {
            foreach (var piece in body.GetComponentsInChildren<GrownPiece>(true))
            {
                bool strands = piece.name == ProceduralHair.StrandsName;
                if ((piece.name != ProceduralHair.RootName && !strands) || !piece.gameObject.activeSelf) continue;
                var mesh = piece.Mesh;
                if (mesh == null || !mesh.isReadable) continue;
                var toWorld = piece.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                // A skinned piece's mesh is as it was built; its first bone is the head, which holds
                // what is wholly its own where the head is now, through its bind pose.
                var skin = strands ? piece.GetComponent<SkinnedMeshRenderer>() : null;
                var bindposes = skin != null ? mesh.bindposes : null;
                if (bindposes != null && bindposes.Length > 0 && skin.bones.Length > 0 && skin.bones[0] != null)
                    toWorld = skin.bones[0].localToWorldMatrix * bindposes[0];
                var weights = strands ? mesh.boneWeights : null;
                bool shared = weights != null && weights.Length == vertices.Length;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (shared && !(weights[i].boneIndex0 == 0 && weights[i].weight0 >= .999f)) continue;
                    points.Add(scan.ToFrame(toWorld.MultiplyPoint3x4(vertices[i])));
                }
            }
        }

        private static float Share(bool[] bones, BoneWeight w)
            => (bones[w.boneIndex0] ? w.weight0 : 0f) + (bones[w.boneIndex1] ? w.weight1 : 0f)
               + (bones[w.boneIndex2] ? w.weight2 : 0f) + (bones[w.boneIndex3] ? w.weight3 : 0f);

        /// <summary>The outermost skin point on one side near a height and depth: the surface of the head there.</summary>
        private static Vector3 SnapToSide(HeadScan scan, int side, float y, float z)
        {
            var best = new Vector3(scan.Centre.x + side * scan.SkullHalfWidth, y, z);
            float reach = -1f;
            foreach (var p in scan.Points)
            {
                if (Mathf.Abs(p.y - y) > .008f || Mathf.Abs(p.z - z) > .016f) continue;
                float out_ = (p.x - scan.Centre.x) * side;
                if (out_ > reach) { reach = out_; best = p; }
            }
            return best;
        }

        /// <summary>The farthest <paramref name="points"/> reach from the head's centre, by direction.</summary>
        private static float[,] Envelope(HeadScan scan, IEnumerable<Vector3> points)
        {
            var grid = new float[EnvelopeRows, EnvelopeColumns];
            foreach (var p in points)
            {
                var d = p - scan.Centre;
                float radius = d.magnitude;
                if (radius < 1e-4f) continue;
                d /= radius;
                int r = Mathf.Clamp(Mathf.FloorToInt(Mathf.Acos(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI * EnvelopeRows), 0, EnvelopeRows - 1);
                int c = Mathf.Clamp(Mathf.FloorToInt((Mathf.Atan2(d.x, d.z) / (Mathf.PI * 2f) + .5f) * EnvelopeColumns), 0, EnvelopeColumns - 1);
                grid[r, c] = Mathf.Max(grid[r, c], radius);
            }
            FillEnvelope(scan, grid);
            return grid;
        }

        /// <summary>Fills directions the head sends nothing along from their neighbours, and softens the rest a little.</summary>
        private static void FillEnvelope(HeadScan scan, float[,] grid)
        {
            for (int pass = 0; pass < 6; pass++)
            {
                bool empty = false;
                for (int r = 0; r < EnvelopeRows; r++)
                    for (int c = 0; c < EnvelopeColumns; c++)
                    {
                        if (grid[r, c] > 0f) continue;
                        float sum = 0f; int n = 0;
                        for (int dr = -1; dr <= 1; dr++)
                            for (int dc = -1; dc <= 1; dc++)
                            {
                                int rr = r + dr; if (rr < 0 || rr >= EnvelopeRows) continue;
                                float v = grid[rr, ((c + dc) % EnvelopeColumns + EnvelopeColumns) % EnvelopeColumns];
                                if (v > 0f) { sum += v; n++; }
                            }
                        if (n > 0) grid[r, c] = sum / n; else empty = true;
                    }
                if (!empty) break;
            }
            float fallback = Mathf.Max(scan.Extent.x, Mathf.Max(scan.Extent.y, scan.Extent.z));
            for (int r = 0; r < EnvelopeRows; r++)
                for (int c = 0; c < EnvelopeColumns; c++)
                    if (grid[r, c] <= 0f) grid[r, c] = fallback;
        }

        internal static Vector3[] SmoothNormals(Vector3[] points, int[] triangles)
        {
            var normals = new Vector3[points.Length];
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                var n = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                normals[a] += n; normals[b] += n; normals[c] += n;
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = normals[i].sqrMagnitude > 0f ? normals[i].normalized : Vector3.up;
            return normals;
        }
    }
}
