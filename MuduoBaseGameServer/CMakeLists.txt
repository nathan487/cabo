cmake_minimum_required(VERSION 3.5)
project(mymoduo)

#mymoduo 最终编译成so动态库，设置动态库的路径
set(LIBRARY_OUTPUT_PATH ${PROJECT_SOURCE_DIR}/lib)
#设置调试信息
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -g -std=c++11")

aux_source_directory(. SRC_LIST)
#编译动态库
add_library(mymuduo SHARED ${SRC_LIST})

# ============================================================
# GameServer target
# ============================================================

# Protobuf — use anaconda path if system lib not available
find_package(Protobuf QUIET)
if(NOT Protobuf_FOUND)
    set(PROTOBUF_INCLUDE_DIR "$ENV{HOME}/anaconda3/include")
    set(PROTOBUF_LIB_DIR "$ENV{HOME}/anaconda3/lib")
    set(PROTOBUF_LIBRARY "$ENV{HOME}/anaconda3/lib/libprotobuf.so")
    message(STATUS "Using anaconda protobuf: ${PROTOBUF_INCLUDE_DIR}")
else()
    set(PROTOBUF_LIB_DIR "${Protobuf_LIBRARY_DIRS}")
    set(PROTOBUF_LIBRARY "${PROTOBUF_LIBRARY}")
    message(STATUS "Using system protobuf: ${PROTOBUF_INCLUDE_DIR}")
endif()

find_package(OpenSSL QUIET)
if(OpenSSL_FOUND)
    message(STATUS "Using OpenSSL crypto: ${OPENSSL_VERSION}")
else()
    message(FATAL_ERROR "OpenSSL crypto is required for WebSocket Sec-WebSocket-Accept SHA1")
endif()

set(PROTO_SRC_DIR "${CMAKE_SOURCE_DIR}/src/proto")
set(PROTO_SCHEMA_DIR "${CMAKE_SOURCE_DIR}/../Proto")

if(NOT Protobuf_PROTOC_EXECUTABLE)
    find_program(Protobuf_PROTOC_EXECUTABLE protoc
        HINTS "$ENV{HOME}/anaconda3/bin")
endif()
if(NOT Protobuf_PROTOC_EXECUTABLE)
    message(FATAL_ERROR "protoc is required to build GameServer")
endif()

set(PROTO_FILES
    ${PROTO_SCHEMA_DIR}/common.proto
    ${PROTO_SCHEMA_DIR}/room.proto
    ${PROTO_SCHEMA_DIR}/game.proto
    ${PROTO_SCHEMA_DIR}/sync.proto
    ${PROTO_SCHEMA_DIR}/messages.proto
)

set(PROTO_SRCS
    ${PROTO_SRC_DIR}/common.pb.cc
    ${PROTO_SRC_DIR}/messages.pb.cc
    ${PROTO_SRC_DIR}/room.pb.cc
    ${PROTO_SRC_DIR}/game.pb.cc
    ${PROTO_SRC_DIR}/sync.pb.cc
)

set(PROTO_HDRS
    ${PROTO_SRC_DIR}/common.pb.h
    ${PROTO_SRC_DIR}/messages.pb.h
    ${PROTO_SRC_DIR}/room.pb.h
    ${PROTO_SRC_DIR}/game.pb.h
    ${PROTO_SRC_DIR}/sync.pb.h
)

add_custom_command(
    OUTPUT ${PROTO_SRCS} ${PROTO_HDRS}
    COMMAND ${Protobuf_PROTOC_EXECUTABLE}
        --cpp_out=${PROTO_SRC_DIR}
        --proto_path=${PROTO_SCHEMA_DIR}
        ${PROTO_FILES}
    DEPENDS ${PROTO_FILES}
    COMMENT "Generating C++ protobuf sources"
    VERBATIM
)

add_custom_target(generate_game_protos DEPENDS ${PROTO_SRCS} ${PROTO_HDRS})

set(GAME_SERVER_SRCS
    src/common/WebSocketCodec.cc
    src/common/MessageCodec.cc
    src/common/MessageDispatcher.cc
    src/room/RoomService.cc
    src/game/GameService.cc
    src/server/GameServer.cc
    ${PROTO_SRCS}
)

add_executable(GameServer ${GAME_SERVER_SRCS})
add_dependencies(GameServer generate_game_protos)

# Protobuf 3.20 anaconda build: InternalMetadata destructor is not exported.
# NDEBUG triggers the inline definition in metadata_lite.h.
target_compile_definitions(GameServer PRIVATE NDEBUG)

target_include_directories(GameServer PRIVATE
    ${CMAKE_SOURCE_DIR}
    ${CMAKE_SOURCE_DIR}/src
    ${PROTO_SRC_DIR}
    ${PROTOBUF_INCLUDE_DIR}
)

target_link_libraries(GameServer
    mymuduo
    ${PROTOBUF_LIBRARY}
    pthread
    OpenSSL::Crypto
)

set_target_properties(GameServer PROPERTIES
    BUILD_WITH_INSTALL_RPATH TRUE
    INSTALL_RPATH "${CMAKE_SOURCE_DIR}/lib"
)

add_executable(websocket_codec_test
    mytest/websocket_codec_test.cpp
    src/common/WebSocketCodec.cc
)
target_include_directories(websocket_codec_test PRIVATE
    ${CMAKE_SOURCE_DIR}
    ${CMAKE_SOURCE_DIR}/src
)
target_link_libraries(websocket_codec_test
    OpenSSL::Crypto
)

add_executable(game_service_regression_test
    mytest/game_service_regression_test.cpp
    src/common/WebSocketCodec.cc
    src/game/GameService.cc
    ${PROTO_SRCS}
)
add_dependencies(game_service_regression_test generate_game_protos)
target_compile_definitions(game_service_regression_test PRIVATE NDEBUG)
target_include_directories(game_service_regression_test PRIVATE
    ${CMAKE_SOURCE_DIR}
    ${CMAKE_SOURCE_DIR}/src
    ${PROTO_SRC_DIR}
    ${PROTOBUF_INCLUDE_DIR}
)
target_link_libraries(game_service_regression_test
    mymuduo
    ${PROTOBUF_LIBRARY}
    pthread
    OpenSSL::Crypto
)

# RPATH intentionally points only to the local muduo library.
# Anaconda protobuf is loaded via LD_PRELOAD (avoids older anaconda libstdc++).

# ============================================================
# Regenerate protobuf C++ code (run manually when .proto changes)
# ============================================================
# Protobuf C++ files are regenerated automatically from ../Proto.

# ============================================================
# How to run:
#   cd build && cmake .. && make -j$(nproc)
#   LD_PRELOAD=$HOME/anaconda3/lib/libprotobuf.so.31 ./GameServer [port]
# ============================================================
