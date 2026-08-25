extends Area2D
## 上から降ってくる障害物。画面外に出たら自分を消す。

var speed := 300.0

func _process(delta: float) -> void:
	position.y += speed * delta
	rotation += delta * 2.0
	if position.y > get_viewport_rect().size.y + 64.0:
		queue_free()
