#include "qmlsharp/qmlsharp_abi.h"

#include <cstdlib>
#include <iostream>

namespace {
int fail(const char* message) {
    std::cerr << message << '\n';
    return EXIT_FAILURE;
}
}  // namespace

int main() {
    if (qmlsharp_get_abi_version() != QMLSHARP_ABI_VERSION) {
        return fail("qmlsharp_get_abi_version returned an unexpected ABI version.");
    }

    qmlsharp_free_string(nullptr);

    const char* last_error = qmlsharp_get_last_error();
    if (last_error != nullptr) {
        qmlsharp_free_string(last_error);
        return fail("qmlsharp_get_last_error should return null before any native error.");
    }

    return EXIT_SUCCESS;
}
