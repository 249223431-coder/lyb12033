import os

folder = r"E:\team\team\static\uploads\MCS图纸\6号涂布机修改原设备的电路图纸"

count = 0
for filename in os.listdir(folder):
    if filename.endswith('.pdf'):
        if 'JOD31EH_9' in filename:
            old_path = os.path.join(folder, filename)
            new_filename = filename.replace('JOD31EH_9', 'AB15TM9')
            new_path = os.path.join(folder, new_filename)
            os.rename(old_path, new_path)
            count += 1
            print(f"重命名: {filename} -> {new_filename}")

print(f"\n完成！共重命名 {count} 个文件")
