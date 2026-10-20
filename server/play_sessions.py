"""Account-owned game processes and format-separated human matchmaking.

One Forge process owns one match. Each human has a separate GUI endpoint and
sees their own seat as seat 1. Raw engine operations are never client-selected.
"""
from dataclasses import dataclass
import threading
import time
import uuid
from .engine import Engine
from .storage import StorageError
from .match_visibility import snapshot_for_player, poll_for_player


@dataclass
class Room:
    id: str
    engine: Engine
    users: tuple
    event: str
    created_at: float
    ready: bool = False


class PlaySessions:
    def __init__(self, prototype, capacity=8):
        self.prototype = prototype
        self.capacity = capacity
        self.lock = threading.RLock()
        self.rooms = {}
        self.assignments = {}
        self.waiting = {}
        self.idle = [prototype] if prototype else []

    def _engine(self):
        if not self.prototype:
            raise StorageError('Rules engine is not configured', 503)
        if self.idle:
            return self.idle.pop()
        return Engine(self.prototype.command, self.prototype.cwd,
                      self.prototype.default_timeout, self.prototype.env)

    def _available(self, users):
        if len(self.rooms) >= self.capacity:
            raise StorageError('All match slots are busy', 503)
        if any(uid in self.assignments for uid in users):
            raise StorageError('Leave your current match before joining another', 409)

    def _reserve(self, users, event):
        self._available(users)
        engine = self._engine()
        room = Room(str(uuid.uuid4()), engine, tuple(users), event, time.time())
        self.rooms[room.id] = room
        for index, uid in enumerate(users):
            self.assignments[uid] = (room.id, index)
            self.waiting.pop(uid, None)
        return room

    def _launch(self, room, decks, human, **options):
        try:
            result = room.engine.request('interactiveStart', decks=decks, humanOpponent=human, **options)
        except Exception:
            room.engine.close()
            with self.lock:
                self.rooms.pop(room.id,None)
                for uid in room.users:
                    if self.assignments.get(uid,(None,))[0]==room.id:self.assignments.pop(uid,None)
            raise
        with self.lock:
            active=self.rooms.get(room.id) is room
            if active:room.ready=True
        if not active:
            room.engine.close()
            raise StorageError('Match was cancelled while starting',409)
        return {**result, 'roomId': room.id, 'event': room.event, 'opponentType': 'human' if human else 'bot'}

    def start_bot(self, uid, decks, event, **options):
        with self.lock:
            room=self._reserve((uid,),event)
        return self._launch(room,decks,False,**options)

    def join(self, uid, deck, event):
        paired=None
        with self.lock:
            if uid in self.assignments:
                return self.status(uid)
            now = time.time()
            self.waiting = {key: value for key, value in self.waiting.items() if now-value[0] < 300}
            for other, (_, other_event, other_deck) in list(self.waiting.items()):
                if other != uid and other_event == event:
                    paired=(self._reserve((other,uid),event),[other_deck,deck])
                    break
            if not paired:
                self.waiting[uid] = (now, event, deck)
                return {'status': 'queued', 'event': event}
        self._launch(paired[0],paired[1],True)
        return self.status(uid)

    def status(self, uid):
        with self.lock:
            assignment = self.assignments.get(uid)
            if assignment:
                room = self.rooms[assignment[0]]
                return {'status': 'matched' if room.ready else 'starting', 'roomId': room.id, 'event': room.event,
                        'opponentType': 'human' if len(room.users)>1 else 'bot', 'playerCount': 2}
            if uid in self.waiting:
                _,event,deck=self.waiting[uid]
                self.waiting[uid]=(time.time(),event,deck)
                return {'status':'queued','event':event}
            return {'status':'idle'}

    def request(self, uid, operation, **fields):
        with self.lock:
            assignment = self.assignments.get(uid)
            if not assignment:
                raise StorageError('No match belongs to this account', 404)
            room = self.rooms[assignment[0]]
            if not room.ready:raise StorageError('Match is still starting',409)
            participant = assignment[1]
        allowed = {'interactiveSnapshot', 'interactivePoll', 'interactiveAction', 'interactiveAnswer'}
        if operation not in allowed:
            raise StorageError('Unsupported match operation', 400)
        fields.pop('participant', None)
        fields.pop('viewerSeat',None)
        result=room.engine.request(operation, participant=participant, **fields)
        if operation=='interactiveSnapshot':return snapshot_for_player(result)
        if operation=='interactivePoll':return poll_for_player(result)
        return result

    def leave(self, uid):
        room=None
        with self.lock:
            self.waiting.pop(uid, None)
            assignment = self.assignments.pop(uid, None)
            if not assignment:
                return {'closed': True}
            room = self.rooms.get(assignment[0])
            if room:
                # Closing a room ends the game for both participants.
                for user in room.users:
                    self.assignments.pop(user, None)
                self.rooms.pop(room.id, None)
        if room:
            try:
                room.engine.request('interactiveClose')
                with self.lock:
                    keep=len(room.users)==1 and room.ready and len(self.idle)<2
                    if keep:self.idle.append(room.engine)
                if not keep:room.engine.close()
            except Exception:
                room.engine.close()
        return {'closed': True}

    def close(self):
        with self.lock:
            for room in self.rooms.values():
                room.engine.close()
            for engine in self.idle:engine.close()
            self.idle.clear()
            self.rooms.clear()
            self.assignments.clear()
            self.waiting.clear()
