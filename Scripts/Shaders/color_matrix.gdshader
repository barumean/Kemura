shader_type canvas_item;

uniform mat4 color_matrix = mat4(1.0);
uniform vec4 color_offset = vec4(0.0);

void fragment() {
    vec4 color = texture(TEXTURE, UV);
    // Apply color matrix transformation (for character image tinting)
    vec4 result;
    result.r = dot(color_matrix[0], color) + color_offset.r;
    result.g = dot(color_matrix[1], color) + color_offset.g;
    result.b = dot(color_matrix[2], color) + color_offset.b;
    result.a = dot(color_matrix[3], color) + color_offset.a;
    COLOR = clamp(result, 0.0, 1.0);
}
