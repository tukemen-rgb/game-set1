extends Area2D
## プレイヤー機体。左右キー（← → / A D は ui_left, ui_right に標準割り当て）で移動する。

const SPEED := 550.0
const HALF_WIDTH := 32.0

func _process(delta: float) -> void:
	var dir := Input.get_axis("ui_left", "ui_right")
	position.x += dir * SPEED * delta
	var view_width := get_viewport_rect().size.x
	position.x = clampf(position.x, HALF_WIDTH, view_width - HALF_WIDTH)
