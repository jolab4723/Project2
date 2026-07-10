from PIL import Image
import os

base_dir = r"c:\Users\user\Desktop\Project2\Assets\SW\Sprites"
files_to_process = ["weapon.png", "chest.png", "helmet.png"]

for file_name in files_to_process:
    path = os.path.join(base_dir, file_name)
    if not os.path.exists(path):
        continue
    img = Image.open(path)
    print(f"{file_name}: mode={img.mode}, size={img.size}")
