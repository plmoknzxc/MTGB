"""Select exact descendants from the frozen face-local payload row format."""


def descendant_nodes(row):
    """The chain is source-line sorted, not a root-first traversal."""
    nodes = row['chain']
    lines = [node['line'] for node in nodes]
    if len(lines) != len(set(lines)):
        raise ValueError(f"{row['id']}: duplicate face-local source line")
    roots = [node for node in nodes if node['line'] == row['effectiveLine']]
    if len(roots) != 1 or roots[0]['text'] != row['root']['text']:
        raise ValueError(f"{row['id']}: root identity does not match chain")
    return [node for node in nodes if node['line'] != row['effectiveLine']]
