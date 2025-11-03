<?php
// Minimal plugin loader for the official Adminer image
class AdminerPlugin {
  /** @var array */
  protected $plugins;
  public function __construct($plugins) { $this->plugins = $plugins; }
  public function __call($name, $args) {
    foreach ($this->plugins as $plugin) {
      if (method_exists($plugin, $name)) {
        return $plugin->$name(...$args);
      }
    }
    return null;
  }
}
function adminer_object() {
  $plugins = [];
  foreach (glob(__DIR__ . "/../plugins-enabled/*.php") as $f) {
    $obj = include $f;
    if ($obj) $plugins[] = $obj;
  }
  return new AdminerPlugin($plugins);
}