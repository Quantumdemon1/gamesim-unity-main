"""Extracts the wall layout the shell is built over: every thin, tall BoxCollider under
"House Architecture" in the shipping scene, with its world centre, size and active state.

    python ArtSource/shell/extract_walls.py

Reads Assets/Gamesim/Scenes/EpisodeHouse.unity directly (Unity's text serialisation), so it
needs no editor. Run it again whenever the shell generator changes a wall, then regenerate
bb_shell_house.fbx with bb_shell.py.
"""
import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
SCENE = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Gamesim", "Scenes", "EpisodeHouse.unity"))
OUT = os.path.join(HERE, "house_walls.json")
IGNORE = {"Television"}   # thin and tall, but not a wall


def vector(body, key):
    m = re.search(key + r": \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", body)
    return [float(m.group(i)) for i in (1, 2, 3)]


def main():
    text = io.open(SCENE, encoding="utf-8").read()
    objects = {}
    for m in re.finditer(r"--- !u!1 &(\d+)\nGameObject:\n(.*?)(?=\n--- !u!)", text, re.S):
        body = m.group(2)
        objects[m.group(1)] = (re.search(r"m_Name: (.*)", body).group(1).strip(),
                               re.search(r"m_IsActive: (\d)", body).group(1) == "1")
    transforms = {}
    for m in re.finditer(r"--- !u!4 &(\d+)\nTransform:\n(.*?)(?=\n--- !u!)", text, re.S):
        body = m.group(2)
        owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", body).group(1)
        transforms[owner] = {"id": m.group(1), "pos": vector(body, "m_LocalPosition"), "scale": vector(body, "m_LocalScale"),
                             "father": re.search(r"m_Father: \{fileID: (\d+)\}", body).group(1)}
    boxes = {}
    for m in re.finditer(r"--- !u!65 &(\d+)\nBoxCollider:\n(.*?)(?=\n--- !u!)", text, re.S):
        body = m.group(2)
        owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", body).group(1)
        boxes[owner] = {"size": vector(body, "m_Size"), "center": vector(body, "m_Center"),
                        "enabled": re.search(r"m_Enabled: (\d)", body).group(1) == "1"}
    by_transform = {v["id"]: k for k, v in transforms.items()}

    def world(owner):
        position = list(transforms[owner]["pos"])
        active = objects[owner][1]
        path = [objects[owner][0]]
        father = transforms[owner]["father"]
        while father != "0":
            parent = by_transform[father]
            position = [position[i] + transforms[parent]["pos"][i] for i in range(3)]
            active = active and objects[parent][1]
            path.append(objects[parent][0])
            father = transforms[parent]["father"]
        return position, active, "/".join(reversed(path))

    walls = []
    for owner, (name, _) in objects.items():
        if owner not in transforms or owner not in boxes or name in IGNORE:
            continue
        scale = transforms[owner]["scale"]
        box = boxes[owner]
        size = [abs(box["size"][i] * scale[i]) for i in range(3)]
        if size[1] < 1.0 or min(size[0], size[2]) > 0.3 or max(size[0], size[2]) < 1.0:
            continue
        position, active, path = world(owner)
        if not path.startswith("House Architecture"):
            continue
        walls.append({
            "name": name,
            "active": active and box["enabled"],
            "kind": "fence" if "fence" in name.lower() else "wall",
            "center": [round(position[i] + box["center"][i] * scale[i], 3) for i in range(3)],
            "size": [round(v, 3) for v in size],
        })
    walls.sort(key=lambda w: (w["center"][2], w["center"][0]))
    io.open(OUT, "w", encoding="utf-8", newline="\n").write(json.dumps({
        "source": "Assets/Gamesim/Scenes/EpisodeHouse.unity",
        "note": "thin tall BoxColliders under House Architecture: the walls, dividers and fences the NavMesh is baked from. Regenerate with extract_walls.py.",
        "walls": walls,
    }, indent=1) + "\n")
    print(len(walls), "walls;", sum(1 for w in walls if not w["active"]), "inactive")


if __name__ == "__main__":
    main()
