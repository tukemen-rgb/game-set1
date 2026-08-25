extends Node2D
## ゲーム全体の進行管理：スタート／スコア加算／障害物の生成／ゲームオーバー。

const OBSTACLE_SCENE := preload("res://scenes/obstacle.tscn")

var score := 0.0
var playing := false

@onready var player: Area2D = $Player
@onready var spawn_timer: Timer = $SpawnTimer
@onready var score_label: Label = $UI/ScoreLabel
@onready var message_label: Label = $UI/MessageLabel

func _ready() -> void:
	player.area_entered.connect(_on_player_hit)
	spawn_timer.timeout.connect(_spawn_obstacle)
	_show_title()

func _process(delta: float) -> void:
	if playing:
		score += delta * 10.0
		score_label.text = "SCORE %d" % int(score)
		# スコアが伸びるほど生成間隔を詰めて難しくする（下限あり）
		spawn_timer.wait_time = maxf(0.25, 0.8 - score / 500.0)
	elif Input.is_action_just_pressed("ui_accept"):
		_start_game()

func _show_title() -> void:
	playing = false
	message_label.text = "OBSTACLE DODGE\n\n← → で移動\nスペース / Enter でスタート"
	message_label.visible = true

func _start_game() -> void:
	for obstacle in get_tree().get_nodes_in_group("obstacles"):
		obstacle.queue_free()
	score = 0.0
	playing = true
	message_label.visible = false
	score_label.text = "SCORE 0"
	var view := get_viewport_rect().size
	player.position = Vector2(view.x / 2.0, view.y - 120.0)
	spawn_timer.start()

func _spawn_obstacle() -> void:
	var obstacle := OBSTACLE_SCENE.instantiate()
	obstacle.add_to_group("obstacles")
	obstacle.position = Vector2(randf_range(40.0, get_viewport_rect().size.x - 40.0), -40.0)
	obstacle.speed = randf_range(260.0, 380.0) + minf(score, 400.0)
	add_child(obstacle)

func _on_player_hit(_area: Area2D) -> void:
	if not playing:
		return
	playing = false
	spawn_timer.stop()
	message_label.text = "GAME OVER\nSCORE %d\n\nスペース / Enter でリトライ" % int(score)
	message_label.visible = true
