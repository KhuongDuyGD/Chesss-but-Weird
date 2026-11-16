"""Optional independent test dependency: python-chess. Never used by the game."""
import io
import json
import pathlib
import re
import sys
import chess
import chess.pgn

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "Logs/match-export-tests")
fixtures = json.loads((root / "fixtures.json").read_text(encoding="utf-8"))
plies = 0
positions = 0
for case in fixtures:
    game = chess.pgn.read_game(io.StringIO(case["pgn"]))
    assert game is not None and not game.errors, (case["name"], game.errors)
    board = game.board()
    assert board.is_valid(), (case["name"], "invalid initial board", board.status())
    assert board.fen(en_passant="fen") == case["positions"][0], (case["name"], "initial FEN mismatch")
    positions += 1
    uci = []
    canonical_san = []
    for node in game.mainline():
        assert board.is_legal(node.move), (case["name"], node.move)
        canonical_san.append(board.san(node.move))
        uci.append(node.move.uci())
        board.push(node.move)
        plies += 1
        expected = case["positions"][len(uci)]
        assert board.fen(en_passant="fen") == expected, (case["name"], len(uci), "intermediate FEN mismatch", expected)
        assert chess.Board(expected).is_valid(), (case["name"], len(uci), "invalid intermediate FEN")
        positions += 1
    assert uci == case["uci"], (case["name"], "move mismatch")
    assert board.fen(en_passant="fen") == case["fen"], (case["name"], board.fen(en_passant="fen"), case["fen"])
    assert chess.Board(case["fen"]).is_valid(), (case["name"], "invalid final FEN")
    assert game.headers["Result"] == case["pgn"].strip().split()[-1]
    movetext = case["pgn"].split("\n\n", 1)[1]
    written_san = [token for token in movetext.split() if not re.fullmatch(r"\d+\.{1,3}|1-0|0-1|1/2-1/2", token)]
    assert written_san == canonical_san, (case["name"], written_san, canonical_san)
    print("PASS independent PGN/SAN/FEN:", case["name"])
print(f"Validated {len(fixtures)} games, {plies} plies and {positions} six-field FEN positions independently with python-chess {chess.__version__}.")
