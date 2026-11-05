"""Merge explicitly owned Unity scene documents into the current working scene."""
import re


class SceneConflict(ValueError):
    pass


def documents(text):
    result = {}
    for match in re.finditer(r'^--- !u!\d+ &(-?\d+)[^\n]*\n.*?(?=^--- !u!|\Z)', text, re.M | re.S):
        if match[1] in result:
            raise SceneConflict('Duplicate scene document: ' + match[1])
        result[match[1]] = match[0]
    if not result:
        raise SceneConflict('No Unity scene documents')
    return result


def validate_references(text):
    ids = documents(text)
    for match in re.finditer(r'\{fileID: (-?\d+)([^}]*)\}', text):
        if match[1] != '0' and 'guid:' not in match[2] and match[1] not in ids:
            raise SceneConflict('Unresolved scene reference: ' + match[1])


def merge_owned(current, baseline, authored, owned_ids):
    """Ignore unrelated authoring reserialization; reject concurrent owned edits."""
    live, before, after = map(documents, (current, baseline, authored))
    owned = set(owned_ids)
    changed = {key for key in owned if before.get(key) != after.get(key)}
    for key in changed:
        if live.get(key) != before.get(key):
            raise SceneConflict('Concurrent edit to owned scene document: ' + key)
    output = current
    for key in changed & before.keys():
        replacement = after.get(key, '')
        replacement = '\n'.join(line.rstrip() for line in replacement.split('\n'))
        output = output.replace(live[key], replacement, 1)
    additions = ''.join('\n'.join(line.rstrip() for line in after[key].split('\n'))
                        for key in after if key in changed and key not in before)
    marker = '--- !u!1660057539 &9223372036854775807'
    if additions:
        if marker not in output:
            raise SceneConflict('Missing scene root document')
        output = output.replace(marker, additions + marker, 1)
    validate_references(output)
    return output
