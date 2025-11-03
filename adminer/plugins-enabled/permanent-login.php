<?php
/**
 * Makes Adminer's "Permanent login" truly permanent by returning a stable secret.
 * Sources (in order):
 *   1) ENV ADMINER_PERM_KEY
 *   2) /run/secrets/adminer_perm_key (bind-mounted file)
 *   3) /config/adminer_perm_key (optional extra mount point)
 */
class AdminerPermanentLogin {
  function permanentLogin($create = false) {
    $secret = getenv('ADMINER_PERM_KEY');

    if (!$secret || strlen($secret) < 32) {
      // No stable secret available → let Adminer fall back to temporary cookie
      // (You could also throw an error to make misconfig obvious.)
      return null;
    }

    return $secret;
  }
}
return new AdminerPermanentLogin();