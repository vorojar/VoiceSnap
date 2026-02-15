package singleinstance

// Lock represents a single-instance lock.
type Lock interface {
	Release()
}
