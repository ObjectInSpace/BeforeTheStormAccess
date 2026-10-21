"""Decode Before the Storm .lsb localization sheets (format: docs\\game-api.md,
"Localization sheets").

Usage: python dump_game_strings.py <StreamingAssets\\Localization> <sheet> <out_dir>
Writes <sheet>.<LANG>.txt (key TAB value) per language, e.g. sheet "global".
Use it to find a game key before adding a mod string (Loc.Game), and to look
up the game's official term when translating. The output is the game's
copyrighted text: keep it out of the repository.
"""
import os
import struct
import sys

KEY = bytes([170, 17, 35, 52, 50, 33, 232, 59, 186, 18, 36, 53, 66, 49, 248, 91,
             202, 19, 37, 54, 82, 161, 8, 123, 218, 20, 38, 55, 98, 193, 120, 155])


def decode(path):
    data = bytearray(open(path, 'rb').read())
    count = struct.unpack_from('<H', data, 0)[0]
    values_rel = struct.unpack_from('<I', data, 2)[0]
    start = count * 3 + 6
    for i in range(len(data) - start):
        data[start + i] ^= KEY[i % len(KEY)]
    key_len_pos = 6
    val_len_pos = key_len_pos + count
    key_pos = val_len_pos + count * 2
    val_pos = values_rel + key_pos
    out = []
    for _ in range(count):
        kl = data[key_len_pos]; key_len_pos += 1
        k = data[key_pos:key_pos + kl].decode('utf-8'); key_pos += kl
        vl = struct.unpack_from('<H', data, val_len_pos)[0]; val_len_pos += 2
        v = data[val_pos:val_pos + vl].decode('utf-8'); val_pos += vl
        out.append((k, v))
    return out


def main():
    root, sheet, out_dir = sys.argv[1], sys.argv[2], sys.argv[3]
    os.makedirs(out_dir, exist_ok=True)
    for lang in sorted(os.listdir(root)):
        path = os.path.join(root, lang, sheet + '.lsb')
        if not os.path.exists(path):
            continue
        rows = decode(path)
        with open(os.path.join(out_dir, f'{sheet}.{lang}.txt'), 'w', encoding='utf-8') as f:
            for k, v in rows:
                f.write(k + '\t' + v.replace('\r', '\\r').replace('\n', '\\n') + '\n')
        print(lang, len(rows))


if __name__ == '__main__':
    main()
