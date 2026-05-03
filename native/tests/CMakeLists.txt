add_executable(qmlsharp_native_tests native_host_smoke_tests.cpp)

target_compile_features(qmlsharp_native_tests PRIVATE cxx_std_20)
target_link_libraries(qmlsharp_native_tests
  PRIVATE
    qmlsharp_native
    Qt6::Core
)

set_target_properties(qmlsharp_native_tests
  PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
)

qmlsharp_apply_warning_settings(qmlsharp_native_tests)
qmlsharp_enable_sanitizers(qmlsharp_native_tests)
qmlsharp_configure_static_analysis(qmlsharp_native_tests)

add_test(NAME qmlsharp_native_tests COMMAND qmlsharp_native_tests)
