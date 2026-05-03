#include "qmlsharp_errors.h"

#include <utility>

namespace qmlsharp {
namespace {
thread_local std::string current_last_error;
}

void set_last_error(std::string message) {
    current_last_error = std::move(message);
}

void clear_last_error() {
    current_last_error.clear();
}

std::string last_error() {
    return current_last_error;
}
}  // namespace qmlsharp
