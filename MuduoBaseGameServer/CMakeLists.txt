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

set(PROTO_SRC_DIR "${CMAKE_SOURCE_DIR}/src/proto")

set(PROTO_SRCS
    ${PROTO_SRC_DIR}/common.pb.cc
    ${PROTO_SRC_DIR}/messages.pb.cc
    ${PROTO_SRC_DIR}/room.pb.cc
    ${PROTO_SRC_DIR}/game.pb.cc
    ${PROTO_SRC_DIR}/sync.pb.cc
)

set(GAME_SERVER_SRCS
    src/common/MessageCodec.cc
    src/common/MessageDispatcher.cc
    src/room/RoomService.cc
    src/game/GameService.cc
    src/server/GameServer.cc
    ${PROTO_SRCS}
)

add_executable(GameServer ${GAME_SERVER_SRCS})

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
)

# RPATH: CMake auto-adds anaconda path since we link against ${PROTOBUF_LIBRARY}.
# Post-build step strips it and sets only muduo lib path.
# Anaconda protobuf loaded via LD_PRELOAD (avoids older anaconda libstdc++).
add_custom_command(TARGET GameServer POST_BUILD
    COMMAND patchelf --set-rpath "${CMAKE_SOURCE_DIR}/lib" "$<TARGET_FILE:GameServer>"
    COMMENT "Fixing RPATH: remove anaconda lib path, keep only muduo lib"
)

# ============================================================
# Regenerate protobuf C++ code (run manually when .proto changes)
# ============================================================
# protoc --cpp_out=src/proto --proto_path=../../Proto \
#   ../../Proto/common.proto ../../Proto/room.proto \
#   ../../Proto/game.proto ../../Proto/sync.proto ../../Proto/messages.proto

# ============================================================
# How to run:
#   cd build && cmake .. && make -j$(nproc)
#   LD_PRELOAD=$HOME/anaconda3/lib/libprotobuf.so.31 ./GameServer [port]
# ============================================================
