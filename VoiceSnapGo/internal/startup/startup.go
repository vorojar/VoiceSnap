package startup

// IsEnabled returns true if the app is set to start at login.
func IsEnabled() bool {
	return isEnabled()
}

// SetEnabled enables or disables start at login.
func SetEnabled(enable bool) error {
	return setEnabled(enable)
}
