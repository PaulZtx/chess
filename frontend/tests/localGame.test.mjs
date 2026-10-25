import { test } from 'node:test'
import assert from 'node:assert/strict'
import { Chess } from 'chess.js'
import { replayGame, exportGame, gameResult } from '../src/lib/localGame.ts'

function coordinates(sans) {
  const chess = new Chess()
  return sans.map((san) => {
    const move = chess.move(san)
    return { from: move.from, to: move.to, promotion: move.promotion }
  })
}

test('both sides can castle, undo restores castling rights, PGN round-trips', () => {
  const moves = coordinates(['e4', 'e5', 'Nf3', 'Nc6', 'Bc4', 'Nf6', 'O-O', 'Be7', 'd3', 'O-O'])
  const chess = replayGame(moves)
  assert.equal(chess.get('g1').type, 'k')
  assert.equal(chess.get('f1').type, 'r')
  assert.equal(chess.get('g8').type, 'k')
  assert.equal(chess.get('f8').type, 'r')
  const undone = replayGame(moves.slice(0, -1))
  assert.equal(undone.get('e8').type, 'k')
  assert.ok(undone.moves().includes('O-O'))
  const imported = new Chess()
  imported.loadPgn(exportGame(moves, new Date(2026, 9, 3)))
  assert.equal(imported.fen(), chess.fen())
  assert.equal(imported.getHeaders().Result, '*')
})

test('en passant removes the captured pawn and undo restores it', () => {
  const moves = coordinates(['e4', 'a6', 'e5', 'd5', 'exd6'])
  assert.equal(replayGame(moves).get('d5'), undefined)
  assert.equal(replayGame(moves).get('d6').type, 'p')
  assert.equal(replayGame(moves.slice(0, -1)).get('d5').type, 'p')
})

test('underpromotion is retained in history and PGN', () => {
  const moves = coordinates(['a4', 'h5', 'a5', 'h4', 'a6', 'h3', 'axb7', 'hxg2', 'bxa8=N'])
  assert.equal(replayGame(moves).get('a8').type, 'n')
  assert.match(exportGame(moves, new Date()), /bxa8=N/)
})

test('illegal moves are rejected, checkmate is exported with the correct result', () => {
  assert.throws(() => replayGame([{ from: 'e2', to: 'e5' }]))
  const moves = coordinates(['f3', 'e5', 'g4', 'Qh4#'])
  assert.equal(gameResult(replayGame(moves)), '0-1')
  const imported = new Chess()
  imported.loadPgn(exportGame(moves, new Date()))
  assert.equal(imported.getHeaders().Result, '0-1')
  assert.ok(imported.isCheckmate())
})
